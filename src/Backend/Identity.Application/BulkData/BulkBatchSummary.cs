using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

public sealed record BulkBatchSummary(
    Guid BatchKey,
    string EntityKey,
    BulkImportSource Source,
    string? FileName,
    BulkImportBatchState State,
    DateTime SubmittedAt,
    DateTime ExpiresAt,
    int TotalRows,
    int CreateRows,
    int UpdateRows,
    int InvalidRows,
    int AppliedRows);
