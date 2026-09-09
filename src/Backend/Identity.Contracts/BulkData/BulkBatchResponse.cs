namespace Identity.Contracts.BulkData;

public sealed record BulkBatchResponse(
    Guid BatchKey,
    string EntityKey,
    string Source,
    string? FileName,
    string State,
    DateTime SubmittedAt,
    DateTime ExpiresAt,
    int TotalRows,
    int CreateRows,
    int UpdateRows,
    int InvalidRows,
    int AppliedRows);
