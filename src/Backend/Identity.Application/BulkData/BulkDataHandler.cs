using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

/// <summary>
/// Stage, preview, correct, commit and discard. An Excel upload and a smart paste both arrive as
/// <see cref="StageBulkBatchCommand"/>, so there is exactly one validation and preview path.
/// </summary>
public sealed class BulkDataHandler(
    IBulkDescriptorCatalog catalog,
    IBulkStagingStore store,
    IBulkRowSerializer serializer,
    IAdministrationStore administrationStore,
    IAdministrationAuthorizer authorizer,
    IUnitOfWork unitOfWork,
    ITransactionRunner transactionRunner,
    BulkValidationPipeline pipeline,
    BulkStagingOptions options,
    TimeProvider timeProvider) :
    IRequestHandler<StageBulkBatchCommand, BulkBatchSummary>,
    IRequestHandler<GetBulkBatchQuery, BulkBatchPage>,
    IRequestHandler<CorrectBulkRowCommand, BulkStagedRow>,
    IRequestHandler<CommitBulkBatchCommand, BulkCommitResult>,
    IRequestHandler<DiscardBulkBatchCommand, BulkDiscardResult>
{
    public async ValueTask<BulkBatchSummary> Handle(
        StageBulkBatchCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorizer.Authorize(request.Context, AdministrationAction.StageBulkImport, cancellationToken)
            .ConfigureAwait(false);

        var definition = Resolve(request.EntityKey);
        if (!definition.Descriptor.AllowsImport)
        {
            throw new AdministrationException(
                $"{definition.Descriptor.DisplayName} is export only and cannot be imported.");
        }

        if (request.Rows.Count == 0)
        {
            throw new ArgumentException("A batch needs at least one row.", nameof(request));
        }

        if (request.Rows.Count > options.Limits.MaxRows)
        {
            throw new BulkDocumentException(
                BulkDocumentError.TooManyRows,
                $"A batch is limited to {options.Limits.MaxRows} rows. Split it into smaller files.");
        }

        var verdicts = definition.Validator is null
            ? []
            : await definition.Validator
                .Verify(definition.Descriptor, request.Rows, cancellationToken)
                .ConfigureAwait(false);
        var outcomes = pipeline.Validate(definition.Descriptor, request.Rows, verdicts);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var batch = BulkImportBatch.Submit(
            Guid.NewGuid(),
            definition.Descriptor.EntityKey,
            definition.Descriptor.TemplateVersion,
            request.Source,
            request.FileName,
            RequireActor(request.Context),
            now,
            options.Retention);

        await store.AddBatch(
            batch,
            batchId => Stage(batchId, request.Rows, outcomes),
            cancellationToken).ConfigureAwait(false);

        Audit(AdministrationAuditEventType.BulkImportSubmitted, request.Context, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await store.Summarize(batch, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<BulkBatchPage> Handle(
        GetBulkBatchQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await Owned(request.BatchKey, request.Context, cancellationToken)
            .ConfigureAwait(false);
        var rows = await store.ListRows(batch.BulkImportBatchId, cancellationToken)
            .ConfigureAwait(false);
        var filtered = request.Filter == BulkRowFilter.NeedsAttention
            ? rows.Where(row => row.State == BulkImportRowState.Invalid).ToArray()
            : rows;

        var take = Math.Clamp(request.Take, 1, 200);
        var page = filtered
            .Skip(Math.Max(request.Skip, 0))
            .Take(take)
            .Select(Project)
            .ToArray();

        return new BulkBatchPage(
            await store.Summarize(batch, cancellationToken).ConfigureAwait(false),
            Math.Max(request.Skip, 0),
            take,
            filtered.Count,
            page);
    }

    public async ValueTask<BulkStagedRow> Handle(
        CorrectBulkRowCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorizer.Authorize(request.Context, AdministrationAction.StageBulkImport, cancellationToken)
            .ConfigureAwait(false);
        var batch = await Owned(request.BatchKey, request.Context, cancellationToken)
            .ConfigureAwait(false);
        RequireStaged(batch);

        var definition = Resolve(batch.EntityKey);
        var row = await store.FindRow(batch.BulkImportBatchId, request.SourceRowNumber, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Staged row was not found.");

        /* Only the corrected row is revalidated, so fixing one cell cannot silently reclassify the
           rest of the batch under data that has since changed. */
        var candidate = ToBulkRow(request.SourceRowNumber, definition.Descriptor, request.Values);
        var verdicts = definition.Validator is null
            ? []
            : await definition.Validator
                .Verify(definition.Descriptor, [candidate], cancellationToken)
                .ConfigureAwait(false);
        var outcome = pipeline.Validate(definition.Descriptor, [candidate], verdicts)[0];
        var now = timeProvider.GetUtcNow().UtcDateTime;

        row.Correct(
            serializer.SerializeValues(request.Values),
            outcome.State == BulkImportRowState.Invalid
                ? serializer.SerializeErrors(outcome.Errors)
                : null,
            outcome.State,
            now);
        Audit(AdministrationAuditEventType.BulkImportRowCorrected, request.Context, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Project(row);
    }

    public async ValueTask<BulkCommitResult> Handle(
        CommitBulkBatchCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorizer.Authorize(request.Context, AdministrationAction.CommitBulkImport, cancellationToken)
            .ConfigureAwait(false);
        var batch = await Owned(request.BatchKey, request.Context, cancellationToken)
            .ConfigureAwait(false);

        /* A replayed commit reports what already happened rather than writing the rows again. */
        if (batch.State == BulkImportBatchState.Committed)
        {
            return new BulkCommitResult(
                batch.BatchKey,
                batch.CreatedRowCount,
                batch.UpdatedRowCount,
                0,
                AlreadyCommitted: true);
        }

        RequireStaged(batch);
        var definition = Resolve(batch.EntityKey);
        if (definition.Committer is null)
        {
            throw new AdministrationException(
                $"Bulk commit is not available for '{batch.EntityKey}'.");
        }

        return await transactionRunner.Execute(
            async token =>
            {
                var rows = await store.ListRows(batch.BulkImportBatchId, token).ConfigureAwait(false);
                var created = 0;
                var updated = 0;
                var now = timeProvider.GetUtcNow().UtcDateTime;

                var ready = rows.Where(candidate => candidate.IsReady).ToList();
                /* A hierarchy needs its parents applied first, wherever the administrator put them in
                   the file, so the committer chooses the order rather than the row numbers. */
                var order = definition.Committer.OrderForCommit(
                    [.. ready.Select(candidate => new BulkCommitCandidate(
                        candidate.SourceRowNumber,
                        serializer.DeserializeValues(candidate.CellValues)))]);
                var byNumber = ready.ToDictionary(candidate => candidate.SourceRowNumber);

                foreach (var sourceRowNumber in order)
                {
                    if (!byNumber.TryGetValue(sourceRowNumber, out var row))
                    {
                        continue;
                    }

                    var isUpdate = row.State == BulkImportRowState.Update;
                    var resourceId = await definition.Committer.Apply(
                        definition.Descriptor,
                        serializer.DeserializeValues(row.CellValues),
                        isUpdate,
                        request.Context,
                        token).ConfigureAwait(false);
                    row.MarkApplied(resourceId, now);
                    if (isUpdate)
                    {
                        updated++;
                    }
                    else
                    {
                        created++;
                    }
                }

                batch.MarkCommitted(created, updated, now);
                Audit(AdministrationAuditEventType.BulkImportCommitted, request.Context, now);
                await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);

                var remaining = rows.Count(candidate => candidate.State == BulkImportRowState.Invalid);
                return new BulkCommitResult(
                    batch.BatchKey, created, updated, remaining, AlreadyCommitted: false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<BulkDiscardResult> Handle(
        DiscardBulkBatchCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await authorizer.Authorize(request.Context, AdministrationAction.StageBulkImport, cancellationToken)
            .ConfigureAwait(false);
        var batch = await Owned(request.BatchKey, request.Context, cancellationToken)
            .ConfigureAwait(false);
        var rows = await store.ListRows(batch.BulkImportBatchId, cancellationToken)
            .ConfigureAwait(false);

        batch.Discard();
        Audit(
            AdministrationAuditEventType.BulkImportDiscarded,
            request.Context,
            timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await store.Remove(batch, cancellationToken).ConfigureAwait(false);
        return new BulkDiscardResult(batch.BatchKey, rows.Count);
    }

    /// <summary>
    /// A bulk batch is owned by a person and must stay attributable, so an unattributed system
    /// context is refused rather than silently recorded against nobody.
    /// </summary>
    private static long RequireActor(AdministrationContext context) =>
        context.ActorUserId
        ?? throw new UnauthorizedAccessException("Bulk data requires an identified administrator.");

    private BulkEntityDefinition Resolve(string entityKey) =>
        catalog.TryResolve(entityKey, out var definition)
            ? definition
            : throw new KeyNotFoundException($"'{entityKey}' does not support bulk data.");

    /// <summary>
    /// Not found, never forbidden: another administrator's batch must be indistinguishable from one
    /// that does not exist, so batch keys cannot be enumerated.
    /// </summary>
    private async Task<BulkImportBatch> Owned(
        Guid batchKey,
        AdministrationContext context,
        CancellationToken cancellationToken) =>
        await store.FindBatch(batchKey, RequireActor(context), cancellationToken).ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Batch was not found.");

    private static void RequireStaged(BulkImportBatch batch)
    {
        if (batch.State != BulkImportBatchState.Staged)
        {
            throw new AdministrationException(
                $"This batch is {batch.State.ToString().ToLowerInvariant()} and can no longer be changed.");
        }
    }

    private List<BulkImportRow> Stage(
        long batchId,
        IReadOnlyList<BulkRow> rows,
        IReadOnlyList<BulkRowOutcome> outcomes)
    {
        var byRow = outcomes.ToDictionary(outcome => outcome.SourceRowNumber);
        var staged = new List<BulkImportRow>(rows.Count);
        foreach (var row in rows)
        {
            var outcome = byRow[row.SourceRowNumber];
            var values = row.Cells.ToDictionary(cell => cell.ColumnId, cell => cell.Value);
            staged.Add(BulkImportRow.Stage(
                batchId,
                row.SourceRowNumber,
                serializer.SerializeValues(values),
                outcome.State == BulkImportRowState.Invalid
                    ? serializer.SerializeErrors(outcome.Errors)
                    : null,
                outcome.State));
        }

        return staged;
    }

    private static BulkRow ToBulkRow(
        int sourceRowNumber,
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values) =>
        new(sourceRowNumber, [.. descriptor.Columns.Select(column =>
        {
            var value = values.GetValueOrDefault(column.ColumnId);
            return new BulkCell(column.ColumnId, value, value, false);
        })]);

    private BulkStagedRow Project(BulkImportRow row) => new(
        row.SourceRowNumber,
        row.State,
        serializer.DeserializeValues(row.CellValues),
        serializer.DeserializeErrors(row.CellErrors));

    private void Audit(
        AdministrationAuditEventType eventType,
        AdministrationContext context,
        DateTime occurredAt) =>
        administrationStore.Add(AuthenticationAudit.CreateAdministrationEvent(
            eventType,
            context.CorrelationId,
            occurredAt,
            userId: context.ActorUserId));
}
