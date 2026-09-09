namespace Identity.Application.BulkData;

/// <summary>Bounds applied before a workbook is parsed. An upload is untrusted input.</summary>
public sealed record BulkDocumentLimits(int MaxBytes, int MaxRows, int MaxSheets)
{
    public static BulkDocumentLimits Default { get; } = new(8 * 1024 * 1024, 5000, 12);
}
