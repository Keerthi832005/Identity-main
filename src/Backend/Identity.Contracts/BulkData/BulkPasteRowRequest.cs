namespace Identity.Contracts.BulkData;

public sealed record BulkPasteRowRequest(
    int SourceRowNumber,
    IReadOnlyDictionary<string, string?> Values);
