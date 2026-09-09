namespace Identity.Application.BulkData;

public interface IBulkWorkbookAnnotator
{
    /// <summary>Marks each failed cell in the original workbook and appends a per-row error summary.</summary>
    Stream Annotate(
        Stream workbook,
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkCellError> errors);
}
