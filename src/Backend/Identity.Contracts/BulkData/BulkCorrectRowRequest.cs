namespace Identity.Contracts.BulkData;

public sealed record BulkCorrectRowRequest(IReadOnlyDictionary<string, string?> Values);
