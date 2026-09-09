namespace Identity.Contracts.BulkData;

public sealed record BulkCellErrorResponse(
    int SourceRowNumber,
    string ColumnId,
    string Code,
    string Message);
