namespace Identity.Contracts.BulkData;

/// <summary>Rows the caller wants exported, in the caller's current column order.</summary>
public sealed record BulkExportRequest(IReadOnlyList<BulkPasteRowRequest> Rows);
