namespace Identity.Application.BulkData;

/// <summary>
/// One read cell. <paramref name="Value"/> is normalised to the declared type in invariant culture,
/// so a date entered as an Excel serial number and one typed as text reach validation identically.
/// <paramref name="SourceText"/> keeps what the user actually entered, for the error message.
/// </summary>
public sealed record BulkCell(
    string ColumnId,
    string? Value,
    string? SourceText,
    bool Malformed);
