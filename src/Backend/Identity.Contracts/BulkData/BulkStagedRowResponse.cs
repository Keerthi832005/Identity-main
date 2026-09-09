namespace Identity.Contracts.BulkData;

public sealed record BulkStagedRowResponse(
    int SourceRowNumber,
    string State,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyList<BulkCellErrorResponse> Errors);
