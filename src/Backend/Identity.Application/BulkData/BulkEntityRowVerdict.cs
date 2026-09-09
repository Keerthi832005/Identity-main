namespace Identity.Application.BulkData;

/// <summary>
/// Per-entity verification of one row: whether a matching record already exists, and any referential
/// failures the descriptor alone cannot detect.
/// </summary>
public sealed record BulkEntityRowVerdict(
    int SourceRowNumber,
    bool MatchesExistingRecord,
    IReadOnlyList<BulkCellError> Errors);
