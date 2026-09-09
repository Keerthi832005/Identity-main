namespace Identity.Application.BulkData;

public sealed record BulkDiscardResult(Guid BatchKey, int DiscardedRowCount);
