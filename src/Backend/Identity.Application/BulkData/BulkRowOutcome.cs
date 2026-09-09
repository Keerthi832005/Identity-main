using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

public sealed record BulkRowOutcome(
    int SourceRowNumber,
    BulkImportRowState State,
    IReadOnlyList<BulkCellError> Errors);
