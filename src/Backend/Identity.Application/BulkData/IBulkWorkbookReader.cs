namespace Identity.Application.BulkData;

public interface IBulkWorkbookReader
{
    /// <summary>Throws <see cref="BulkDocumentException"/> for a structurally unusable workbook.</summary>
    BulkWorkbookReadResult Read(
        Stream workbook,
        BulkEntityDescriptor descriptor,
        BulkDocumentLimits limits);
}
