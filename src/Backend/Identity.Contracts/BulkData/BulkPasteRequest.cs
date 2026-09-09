namespace Identity.Contracts.BulkData;

/// <summary>Smart paste staging. Posts to the same endpoint an Excel upload does.</summary>
public sealed record BulkPasteRequest(IReadOnlyList<BulkPasteRowRequest> Rows);
