using Identity.Application.BulkData;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class BulkStagingStore(IdentityDbContext context) : IBulkStagingStore
{
    public async Task<BulkImportBatch> AddBatch(
        BulkImportBatch batch,
        Func<long, IReadOnlyList<BulkImportRow>> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(rows);
        context.BulkImportBatches.Add(batch);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.BulkImportRows.AddRange(rows(batch.BulkImportBatchId));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return batch;
    }

    /// <summary>
    /// Ownership is part of the lookup, not a check after it: a batch belonging to another
    /// administrator returns null, so batch keys cannot be probed for existence.
    /// </summary>
    public Task<BulkImportBatch?> FindBatch(
        Guid batchKey,
        long ownerUserId,
        CancellationToken cancellationToken) =>
        context.BulkImportBatches
            .FirstOrDefaultAsync(
                batch => batch.BatchKey == batchKey && batch.SubmittedByUserId == ownerUserId,
                cancellationToken);

    public async Task<IReadOnlyList<BulkImportRow>> ListRows(
        long batchId,
        CancellationToken cancellationToken) =>
        await context.BulkImportRows
            .Where(row => row.BulkImportBatchId == batchId)
            .OrderBy(row => row.SourceRowNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<BulkImportRow?> FindRow(
        long batchId,
        int sourceRowNumber,
        CancellationToken cancellationToken) =>
        context.BulkImportRows
            .FirstOrDefaultAsync(
                row => row.BulkImportBatchId == batchId && row.SourceRowNumber == sourceRowNumber,
                cancellationToken);

    public async Task<BulkBatchSummary> Summarize(
        BulkImportBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var counts = await context.BulkImportRows
            .Where(row => row.BulkImportBatchId == batch.BulkImportBatchId)
            .GroupBy(row => row.State)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int CountOf(BulkImportRowState state) =>
            counts.FirstOrDefault(entry => entry.Key == state)?.Count ?? 0;

        return new BulkBatchSummary(
            batch.BatchKey,
            batch.EntityKey,
            batch.Source,
            batch.FileName,
            batch.State,
            batch.SubmittedAt,
            batch.ExpiresAt,
            counts.Sum(entry => entry.Count),
            CountOf(BulkImportRowState.Create),
            CountOf(BulkImportRowState.Update),
            CountOf(BulkImportRowState.Invalid),
            CountOf(BulkImportRowState.Applied));
    }

    public async Task<IReadOnlyList<BulkImportBatch>> ListExpired(
        DateTime asOf,
        int take,
        CancellationToken cancellationToken) =>
        await context.BulkImportBatches
            .Where(batch => batch.State == BulkImportBatchState.Staged && batch.ExpiresAt <= asOf)
            .OrderBy(batch => batch.ExpiresAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task Remove(BulkImportBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        context.BulkImportBatches.Remove(batch);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
