namespace Identity.Application.BulkData;

/// <summary>One template column. <paramref name="ColumnId"/> is the stable identity; header text may change between versions.</summary>
public sealed record BulkColumnDefinition(
    string ColumnId,
    string Header,
    BulkColumnType Type,
    bool Required,
    int WidthInCharacters = 22,
    string? HelpText = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? ReferenceListKey = null,
    int? MaxLength = null);
