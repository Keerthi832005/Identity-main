using System.Drawing;
using DevExpress.Spreadsheet;
using Identity.Application.BulkData;

namespace Identity.Infrastructure.Documents;

/// <summary>
/// Marks failed cells in the administrator's own workbook and appends a per-row summary, so the
/// correction happens in the file they already have rather than against a separate report.
/// </summary>
public sealed class ExcelWorkbookAnnotator : IBulkWorkbookAnnotator
{
    private static readonly Color ErrorFill = Color.FromArgb(0xFD, 0xEE, 0xE6);
    private static readonly Color ErrorText = Color.FromArgb(0xA1, 0x3C, 0x08);
    private const string CommentAuthor = "Identity Administration";

    public Stream Annotate(
        Stream workbook,
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkCellError> errors)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(errors);

        using var document = new Workbook();
        document.LoadDocument(workbook, DocumentFormat.Xlsx);

        var sheet = document.Worksheets
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                ExcelWorkbookLayout.DataSheetName,
                StringComparison.OrdinalIgnoreCase))
            ?? document.Worksheets[0];

        document.BeginUpdate();
        try
        {
            var positions = MapColumns(sheet, descriptor);
            var errorColumn = AddErrorColumn(sheet, descriptor);

            foreach (var group in errors.GroupBy(error => error.SourceRowNumber))
            {
                var rowIndex = group.Key - 1;
                foreach (var error in group)
                {
                    if (!positions.TryGetValue(error.ColumnId, out var column))
                    {
                        continue;
                    }

                    var cell = sheet.Cells[rowIndex, column];
                    cell.Fill.BackgroundColor = ErrorFill;
                    cell.Font.Color = ErrorText;
                    sheet.Comments.Add(cell, CommentAuthor, error.Message);
                }

                var summary = sheet.Cells[rowIndex, errorColumn];
                summary.SetValue(string.Join(" ", group.Select(error => error.Message)));
                summary.Font.Color = ErrorText;
                summary.Alignment.WrapText = true;
            }
        }
        finally
        {
            document.EndUpdate();
        }

        var stream = new MemoryStream();
        document.SaveDocument(stream, DocumentFormat.Xlsx);
        stream.Position = 0;
        return stream;
    }

    private static int AddErrorColumn(Worksheet sheet, BulkEntityDescriptor descriptor)
    {
        var column = descriptor.Columns.Count;
        var header = sheet.Cells[ExcelWorkbookLayout.HeaderRowIndex, column];
        header.Value = ExcelWorkbookLayout.ErrorColumnHeader;
        header.Font.Bold = true;
        header.Font.Color = ErrorText;
        header.Fill.BackgroundColor = ErrorFill;
        sheet.Columns[column].WidthInCharacters = 60;
        return column;
    }

    private static Dictionary<string, int> MapColumns(
        Worksheet sheet,
        BulkEntityDescriptor descriptor)
    {
        var used = sheet.GetUsedRange();
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var column = 0; column < used.ColumnCount; column++)
        {
            var normalised = Normalise(
                sheet.Cells[ExcelWorkbookLayout.HeaderRowIndex, column].Value.TextValue);
            if (normalised.Length > 0 && !headers.ContainsKey(normalised))
            {
                headers[normalised] = column;
            }
        }

        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var definition in descriptor.Columns)
        {
            if (headers.TryGetValue(Normalise(definition.Header), out var index)
                || headers.TryGetValue(Normalise(definition.ColumnId), out index))
            {
                positions[definition.ColumnId] = index;
            }
        }

        return positions;
    }

    private static string Normalise(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var trimmed = header.Trim().TrimEnd('*').Trim();
        return string.Concat(trimmed.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }
}
