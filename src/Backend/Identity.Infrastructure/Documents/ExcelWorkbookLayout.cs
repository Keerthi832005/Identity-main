namespace Identity.Infrastructure.Documents;

/// <summary>
/// One layout shared by the writer, reader and annotator. Every offset lives here so the three
/// cannot drift apart and silently read the wrong row.
/// </summary>
internal static class ExcelWorkbookLayout
{
    internal const string DataSheetName = "Data";
    internal const string ReferenceSheetName = "Reference";
    internal const string MetadataSheetName = "_meta";
    internal const string ErrorColumnHeader = "Errors";

    internal const int InstructionRowIndex = 0;
    internal const int HeaderRowIndex = 1;
    internal const int FirstDataRowIndex = 2;

    internal const string EntityKeyLabel = "entityKey";
    internal const string TemplateVersionLabel = "templateVersion";
    internal const string ColumnIdsLabel = "columnIds";

    /// <summary>Excel's own row numbering, which is what an error message must quote.</summary>
    internal static int ToSourceRowNumber(int rowIndex) => rowIndex + 1;
}
