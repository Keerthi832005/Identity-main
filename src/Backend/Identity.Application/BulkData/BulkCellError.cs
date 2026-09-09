namespace Identity.Application.BulkData;

/// <summary>
/// One validation failure. The same instance drives the preview grid, the API response and the
/// annotated workbook, so an error is never described twice in two places.
/// </summary>
public sealed record BulkCellError(
    int SourceRowNumber,
    string ColumnId,
    string Code,
    string Message);
