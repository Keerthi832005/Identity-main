using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

public sealed record BulkStagedRow(
    int SourceRowNumber,
    BulkImportRowState State,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyList<BulkCellError> Errors);
