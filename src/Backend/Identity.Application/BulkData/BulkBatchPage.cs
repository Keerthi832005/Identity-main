namespace Identity.Application.BulkData;

public sealed record BulkBatchPage(
    BulkBatchSummary Summary,
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<BulkStagedRow> Rows);
