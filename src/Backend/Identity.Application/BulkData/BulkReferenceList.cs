namespace Identity.Application.BulkData;

/// <summary>Lookup values written to the Reference sheet and bound to a column's dropdown.</summary>
public sealed record BulkReferenceList(string Key, IReadOnlyList<string> Values);
