using System.Globalization;
using Identity.Api.Administration;
using Identity.Api.Hosting;
using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Application.Messaging;
using Identity.Contracts.BulkData;
using Identity.Domain.Enums;

namespace Identity.Api.Endpoints;

/// <summary>
/// Template download, export, staging, preview, correction, annotated errors, commit and discard.
/// An upload and a smart paste both stage through the same endpoint, so validation and preview can
/// never diverge between the two entry points.
/// </summary>
internal static class BulkDataEndpoints
{
    private const string WorkbookContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapBulkDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/admin/bulk")
            .RequireAuthorization(AdministrationPolicy.Name);

        group.MapGet("/{entity}/template", Template);
        group.MapPost("/{entity}/export", Export);
        group.MapPost("/{entity}/staging", Stage).DisableAntiforgery();
        group.MapGet("/staging/{batchKey:guid}", GetBatch);
        group.MapPut("/staging/{batchKey:guid}/rows/{sourceRowNumber:int}", CorrectRow);
        group.MapGet("/staging/{batchKey:guid}/errors", Errors);
        group.MapPost("/staging/{batchKey:guid}/commit", Commit);
        group.MapDelete("/staging/{batchKey:guid}", Discard);
        return endpoints;
    }

    private static IResult Template(
        string entity,
        IBulkDescriptorCatalog catalog,
        IBulkWorkbookWriter writer)
    {
        var descriptor = Resolve(catalog, entity).Descriptor;
        var stream = writer.CreateTemplate(descriptor, []);
        return Workbook(stream, descriptor.EntityKey, "template");
    }

    private static IResult Export(
        string entity,
        BulkExportRequest request,
        IBulkDescriptorCatalog catalog,
        IBulkWorkbookWriter writer)
    {
        ArgumentNullException.ThrowIfNull(request);
        var descriptor = Resolve(catalog, entity).Descriptor;
        var rows = request.Rows
            .Select(row => new BulkExportRow(row.Values))
            .ToArray();
        return Workbook(
            writer.CreateExport(descriptor, [], rows),
            descriptor.EntityKey,
            "export");
    }

    private static async Task<IResult> Stage(
        string entity,
        HttpContext context,
        IBulkDescriptorCatalog catalog,
        IBulkWorkbookReader reader,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var descriptor = Resolve(catalog, entity).Descriptor;

        /* Refused at the boundary, before a command is built. The handler repeats the check as
           defence in depth, but an append-only record should never reach the pipeline at all. */
        if (!descriptor.AllowsImport)
        {
            throw new AdministrationException(
                $"{descriptor.DisplayName} is export only and cannot be imported.");
        }

        var administration = RequestContextFactory.Administration(context);

        BulkImportSource source;
        string? fileName;
        IReadOnlyList<BulkRow> rows;

        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files.GetFile("workbook")
                ?? form.Files.FirstOrDefault()
                ?? throw new ArgumentException("A workbook file is required.");
            RejectUnlessWorkbook(file);
            await using var upload = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await upload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            rows = reader.Read(buffer, descriptor, BulkDocumentLimits.Default).Rows;
            source = BulkImportSource.Excel;
            fileName = SanitizeFileName(file.FileName);
        }
        else
        {
            var paste = await context.Request
                .ReadFromJsonAsync<BulkPasteRequest>(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new ArgumentException("Pasted rows are required.");
            rows = paste.Rows
                .Select(row => ToBulkRow(descriptor, row))
                .ToArray();
            source = BulkImportSource.Paste;
            fileName = null;
        }

        var summary = await dispatcher.Send(
            new StageBulkBatchCommand(descriptor.EntityKey, source, fileName, rows, administration),
            cancellationToken);
        return Results.Created($"/api/v1/admin/bulk/staging/{summary.BatchKey:D}", Map(summary));
    }

    private static async Task<IResult> GetBatch(
        Guid batchKey,
        string? filter,
        int? skip,
        int? take,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var page = await dispatcher.Send(
            new GetBulkBatchQuery(
                batchKey,
                skip ?? 0,
                take ?? 50,
                string.Equals(filter, "needs-attention", StringComparison.OrdinalIgnoreCase)
                    ? BulkRowFilter.NeedsAttention
                    : BulkRowFilter.All,
                RequestContextFactory.Administration(context)),
            cancellationToken);

        return Results.Ok(new PagedBulkRowsResponse(
            Map(page.Summary),
            page.Skip,
            page.Take,
            page.TotalCount,
            [.. page.Rows.Select(Map)]));
    }

    private static async Task<IResult> CorrectRow(
        Guid batchKey,
        int sourceRowNumber,
        BulkCorrectRowRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = await dispatcher.Send(
            new CorrectBulkRowCommand(
                batchKey,
                sourceRowNumber,
                request.Values,
                RequestContextFactory.Administration(context)),
            cancellationToken);
        return Results.Ok(Map(row));
    }

    private static async Task<IResult> Errors(
        Guid batchKey,
        HttpContext context,
        IBulkDescriptorCatalog catalog,
        IBulkWorkbookWriter writer,
        IBulkWorkbookAnnotator annotator,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var page = await dispatcher.Send(
            new GetBulkBatchQuery(
                batchKey,
                0,
                BulkDocumentLimits.Default.MaxRows,
                BulkRowFilter.All,
                RequestContextFactory.Administration(context)),
            cancellationToken);

        var descriptor = Resolve(catalog, page.Summary.EntityKey).Descriptor;
        var rows = page.Rows.Select(row => new BulkExportRow(row.Values)).ToArray();
        var errors = page.Rows.SelectMany(row => row.Errors).ToArray();

        /* Rebuilt from the staged rows rather than the uploaded file: corrections made in the preview
           are already reflected, and the original upload is not retained. */
        using var workbook = writer.CreateExport(descriptor, [], rows);
        var annotated = annotator.Annotate(workbook, descriptor, errors);
        return Workbook(annotated, descriptor.EntityKey, "errors");
    }

    private static async Task<IResult> Commit(
        Guid batchKey,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new CommitBulkBatchCommand(batchKey, RequestContextFactory.Administration(context)),
            cancellationToken);
        return Results.Ok(new BulkCommitResponse(
            result.BatchKey,
            result.CreatedRowCount,
            result.UpdatedRowCount,
            result.RemainingInvalidRowCount,
            result.AlreadyCommitted));
    }

    private static async Task<IResult> Discard(
        Guid batchKey,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new DiscardBulkBatchCommand(batchKey, RequestContextFactory.Administration(context)),
            cancellationToken);
        return Results.Ok(new BulkDiscardResponse(result.BatchKey, result.DiscardedRowCount));
    }

    private static BulkEntityDefinition Resolve(IBulkDescriptorCatalog catalog, string entity) =>
        catalog.TryResolve(entity, out var definition)
            ? definition
            : throw new KeyNotFoundException($"'{entity}' does not support bulk data.");

    /// <summary>Rejected before the workbook is opened: an upload is untrusted input.</summary>
    private static void RejectUnlessWorkbook(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > BulkDocumentLimits.Default.MaxBytes)
        {
            throw new BulkDocumentException(
                BulkDocumentError.TooLarge,
                $"The file must be between 1 byte and {BulkDocumentLimits.Default.MaxBytes / (1024 * 1024)} MB.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new BulkDocumentException(
                BulkDocumentError.NotAWorkbook,
                "Upload the .xlsx template. Other formats are not accepted.");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var safe = string.Concat(name.Where(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ' '));
        return safe.Length == 0 ? "upload.xlsx" : safe[..Math.Min(safe.Length, 260)];
    }

    private static IResult Workbook(Stream stream, string entityKey, string kind)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Results.File(stream, WorkbookContentType, $"{entityKey}-{kind}-{stamp}.xlsx");
    }

    private static BulkRow ToBulkRow(BulkEntityDescriptor descriptor, BulkPasteRowRequest row) =>
        new(row.SourceRowNumber, [.. descriptor.Columns.Select(column =>
        {
            var value = row.Values.GetValueOrDefault(column.ColumnId);
            return new BulkCell(column.ColumnId, value, value, false);
        })]);

    private static BulkBatchResponse Map(BulkBatchSummary summary) => new(
        summary.BatchKey,
        summary.EntityKey,
        summary.Source.ToString(),
        summary.FileName,
        summary.State.ToString(),
        summary.SubmittedAt,
        summary.ExpiresAt,
        summary.TotalRows,
        summary.CreateRows,
        summary.UpdateRows,
        summary.InvalidRows,
        summary.AppliedRows);

    private static BulkStagedRowResponse Map(BulkStagedRow row) => new(
        row.SourceRowNumber,
        row.State.ToString(),
        row.Values,
        [.. row.Errors.Select(error => new BulkCellErrorResponse(
            error.SourceRowNumber, error.ColumnId, error.Code, error.Message))]);
}
