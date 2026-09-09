namespace Identity.Application.BulkData;

/// <summary>
/// <paramref name="AlreadyCommitted"/> distinguishes a replayed commit from a fresh one, so a retry
/// reports honestly instead of appearing to have written the rows a second time.
/// </summary>
public sealed record BulkCommitResult(
    Guid BatchKey,
    int CreatedRowCount,
    int UpdatedRowCount,
    int RemainingInvalidRowCount,
    bool AlreadyCommitted);
