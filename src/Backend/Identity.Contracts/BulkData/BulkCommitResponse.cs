namespace Identity.Contracts.BulkData;

public sealed record BulkCommitResponse(
    Guid BatchKey,
    int CreatedRowCount,
    int UpdatedRowCount,
    int RemainingInvalidRowCount,
    bool AlreadyCommitted);
