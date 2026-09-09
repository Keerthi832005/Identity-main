namespace Identity.Application.BulkData;

/// <summary>One row to write, keyed by column id. A missing key writes an empty cell.</summary>
public sealed record BulkExportRow(IReadOnlyDictionary<string, string?> Values);
