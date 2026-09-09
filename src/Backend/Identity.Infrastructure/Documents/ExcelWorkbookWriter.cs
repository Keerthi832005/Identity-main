using System.Drawing;
using System.Globalization;
using DevExpress.Spreadsheet;
using Identity.Application.BulkData;

namespace Identity.Infrastructure.Documents;

/// <summary>
/// Builds the template and the populated export from one code path, so an export is always a valid
/// template: an administrator can export a working set, edit it in Excel and re-import it unchanged.
/// </summary>
public sealed class ExcelWorkbookWriter : IBulkWorkbookWriter
{
    private static readonly Color HeaderFill = Color.FromArgb(0xF4, 0xF4, 0xF6);
    private static readonly Color HeaderText = Color.FromArgb(0x1F, 0x21, 0x26);
    private static readonly Color RequiredText = Color.FromArgb(0xD0, 0x11, 0x26);
    private static readonly Color InstructionFill = Color.FromArgb(0xFB, 0xE9, 0xEB);

    public Stream CreateTemplate(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references) =>
        Build(descriptor, references, []);

    public Stream CreateExport(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references,
        IReadOnlyList<BulkExportRow> rows) =>
        Build(descriptor, references, rows);

    private static Stream Build(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references,
        IReadOnlyList<BulkExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new Workbook();
        workbook.BeginUpdate();
        try
        {
            var data = workbook.Worksheets[0];
            data.Name = ExcelWorkbookLayout.DataSheetName;
            WriteInstruction(data, descriptor);
            WriteHeaders(data, descriptor);
            WriteRows(data, descriptor, rows);

            var reference = WriteReferences(workbook, descriptor, references);
            ApplyValidation(data, descriptor, reference, rows.Count);
            WriteMetadata(workbook, descriptor);

            data.FreezeRows(ExcelWorkbookLayout.HeaderRowIndex);
        }
        finally
        {
            workbook.EndUpdate();
        }

        var stream = new MemoryStream();
        workbook.SaveDocument(stream, DocumentFormat.Xlsx);
        stream.Position = 0;
        return stream;
    }

    private static void WriteInstruction(Worksheet sheet, BulkEntityDescriptor descriptor)
    {
        var band = sheet.Range.FromLTRB(
            0,
            ExcelWorkbookLayout.InstructionRowIndex,
            Math.Max(descriptor.Columns.Count - 1, 0),
            ExcelWorkbookLayout.InstructionRowIndex);
        band.Merge();
        band.Value = descriptor.Instruction;
        band.Fill.BackgroundColor = InstructionFill;
        band.Alignment.WrapText = true;
        band.Alignment.Vertical = SpreadsheetVerticalAlignment.Center;
        /* Excel does not auto-fit a merged wrapped cell, so the band gets an explicit height. */
        sheet.Rows[ExcelWorkbookLayout.InstructionRowIndex].Height = 340;
    }

    private static void WriteHeaders(Worksheet sheet, BulkEntityDescriptor descriptor)
    {
        for (var column = 0; column < descriptor.Columns.Count; column++)
        {
            var definition = descriptor.Columns[column];
            var cell = sheet.Cells[ExcelWorkbookLayout.HeaderRowIndex, column];
            cell.Value = definition.Required ? definition.Header + " *" : definition.Header;
            cell.Font.Bold = true;
            cell.Font.Color = definition.Required ? RequiredText : HeaderText;
            cell.Fill.BackgroundColor = HeaderFill;
            cell.Borders.BottomBorder.LineStyle = BorderLineStyle.Thin;

            if (!string.IsNullOrWhiteSpace(definition.HelpText))
            {
                sheet.Comments.Add(cell, descriptor.DisplayName, definition.HelpText);
            }

            sheet.Columns[column].WidthInCharacters = definition.WidthInCharacters;
        }
    }

    private static void WriteRows(
        Worksheet sheet,
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkExportRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            for (var column = 0; column < descriptor.Columns.Count; column++)
            {
                var columnId = descriptor.Columns[column].ColumnId;
                if (!row.Values.TryGetValue(columnId, out var value) || value is null)
                {
                    continue;
                }

                /* Written as text so a code such as 00123 or a leading-plus phone number survives the
                   round trip instead of being coerced to a number by Excel. */
                sheet.Cells[ExcelWorkbookLayout.FirstDataRowIndex + index, column].SetValue(value);
            }
        }
    }

    private static Worksheet WriteReferences(
        Workbook workbook,
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references)
    {
        var sheet = workbook.Worksheets.Add();
        sheet.Name = ExcelWorkbookLayout.ReferenceSheetName;

        var lists = BuildReferenceLists(descriptor, references);
        var column = 0;
        foreach (var list in lists)
        {
            sheet.Cells[0, column].Value = list.Key;
            sheet.Cells[0, column].Font.Bold = true;
            for (var index = 0; index < list.Values.Count; index++)
            {
                sheet.Cells[index + 1, column].SetValue(list.Values[index]);
            }

            sheet.Columns[column].WidthInCharacters = 28;
            column++;
        }

        return sheet;
    }

    /// <summary>
    /// A column's own AllowedValues win over a runtime list of the same key: a fixed enumeration such
    /// as a unit type must never widen because a lookup query returned something unexpected.
    /// </summary>
    private static List<BulkReferenceList> BuildReferenceLists(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkReferenceList> references)
    {
        var lists = new List<BulkReferenceList>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in descriptor.Columns)
        {
            if (definition.AllowedValues is { Count: > 0 } allowed && seen.Add(definition.ColumnId))
            {
                lists.Add(new BulkReferenceList(definition.ColumnId, allowed));
            }
        }

        foreach (var list in references)
        {
            if (list.Values.Count > 0 && seen.Add(list.Key))
            {
                lists.Add(list);
            }
        }

        return lists;
    }

    private static void ApplyValidation(
        Worksheet data,
        BulkEntityDescriptor descriptor,
        Worksheet reference,
        int populatedRowCount)
    {
        var lastRow = ExcelWorkbookLayout.FirstDataRowIndex
            + Math.Max(populatedRowCount, BulkDocumentLimits.Default.MaxRows)
            - 1;

        for (var column = 0; column < descriptor.Columns.Count; column++)
        {
            var definition = descriptor.Columns[column];
            var key = definition.AllowedValues is { Count: > 0 }
                ? definition.ColumnId
                : definition.ReferenceListKey;
            if (key is null)
            {
                continue;
            }

            var source = FindReferenceRange(reference, key);
            if (source is null)
            {
                continue;
            }

            var target = data.Range.FromLTRB(
                column,
                ExcelWorkbookLayout.FirstDataRowIndex,
                column,
                lastRow);
            /* Excel-side validation is a convenience only; blanks stay allowed here because required
               columns are enforced by the server pipeline, which is the authority. */
            var validation = data.DataValidations.Add(target, DataValidationType.List, source);
            validation.ShowInputMessage = false;
        }
    }

    private static string? FindReferenceRange(Worksheet reference, string key)
    {
        var used = reference.GetUsedRange();
        for (var column = 0; column < used.ColumnCount; column++)
        {
            if (!string.Equals(reference.Cells[0, column].Value.TextValue, key, StringComparison.Ordinal))
            {
                continue;
            }

            var lastRow = used.RowCount - 1;
            if (lastRow < 1)
            {
                return null;
            }

            var letter = ColumnLetter(column);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"={ExcelWorkbookLayout.ReferenceSheetName}!${letter}$2:${letter}${lastRow + 1}");
        }

        return null;
    }

    private static string ColumnLetter(int columnIndex)
    {
        var letters = string.Empty;
        var value = columnIndex;
        do
        {
            letters = (char)('A' + (value % 26)) + letters;
            value = (value / 26) - 1;
        }
        while (value >= 0);

        return letters;
    }

    /// <summary>
    /// Column ids, not positions, are the identity of a column. Recording them here is what lets the
    /// reader accept a workbook whose columns an administrator reordered in Excel.
    /// </summary>
    private static void WriteMetadata(Workbook workbook, BulkEntityDescriptor descriptor)
    {
        var sheet = workbook.Worksheets.Add();
        sheet.Name = ExcelWorkbookLayout.MetadataSheetName;
        sheet.Cells[0, 0].Value = ExcelWorkbookLayout.EntityKeyLabel;
        sheet.Cells[0, 1].SetValue(descriptor.EntityKey);
        sheet.Cells[1, 0].Value = ExcelWorkbookLayout.TemplateVersionLabel;
        sheet.Cells[1, 1].SetValue(
            descriptor.TemplateVersion.ToString(CultureInfo.InvariantCulture));
        sheet.Cells[2, 0].Value = ExcelWorkbookLayout.ColumnIdsLabel;
        sheet.Cells[2, 1].SetValue(
            string.Join(',', descriptor.Columns.Select(column => column.ColumnId)));
        sheet.Visible = false;
    }
}
