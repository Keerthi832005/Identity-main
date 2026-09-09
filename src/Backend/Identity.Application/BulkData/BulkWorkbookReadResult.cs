namespace Identity.Application.BulkData;

public sealed record BulkWorkbookReadResult(
    string EntityKey,
    int TemplateVersion,
    IReadOnlyList<BulkRow> Rows);
