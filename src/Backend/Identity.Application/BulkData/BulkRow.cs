namespace Identity.Application.BulkData;

public sealed record BulkRow(int SourceRowNumber, IReadOnlyList<BulkCell> Cells);
