using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

/// <summary>
/// One staging request. An Excel upload and a smart paste produce the same shape, which is why both
/// entry points share a single validation and preview path.
/// </summary>
public sealed record BulkSubmission(
    string EntityKey,
    BulkImportSource Source,
    string? FileName,
    long SubmittedByUserId,
    IReadOnlyList<BulkRow> Rows);
