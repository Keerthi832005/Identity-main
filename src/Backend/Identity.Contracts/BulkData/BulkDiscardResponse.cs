namespace Identity.Contracts.BulkData;

public sealed record BulkDiscardResponse(Guid BatchKey, int DiscardedRowCount);
