namespace Identity.Application.BulkData;

/// <summary>
/// Produces the empty template and the populated export. Both come from one implementation, so an
/// export is always a valid template and can be edited and re-imported without change.
/// </summary>
public interface IBulkWorkbookWriter
{
    Stream CreateTemplate(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references);

    Stream CreateExport(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references,
        IReadOnlyList<BulkExportRow> rows);
}
