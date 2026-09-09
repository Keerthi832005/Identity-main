namespace Identity.Contracts.BulkData;

public sealed record PagedBulkRowsResponse(
    BulkBatchResponse Batch,
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<BulkStagedRowResponse> Items);
