using System.Globalization;
using DevExpress.Spreadsheet;
using Identity.Application.BulkData;

namespace Identity.Infrastructure.Documents;

/// <summary>
/// Reads an uploaded workbook into typed rows. Columns are located by the ids recorded on the hidden
/// metadata sheet, then by header text, so a workbook whose columns were reordered in Excel still
/// imports correctly.
/// </summary>
public sealed class ExcelWorkbookReader : IBulkWorkbookReader
{
    private static readonly string[] TrueWords = ["true", "yes", "y", "1", "active"];
    private static readonly string[] FalseWords = ["false", "no", "n", "0", "inactive"];

    public BulkWorkbookReadResult Read(
        Stream workbook,
        BulkEntityDescriptor descriptor,
        BulkDocumentLimits limits)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(limits);

        if (workbook.CanSeek && workbook.Length > limits.MaxBytes)
        {
            throw new BulkDocumentException(
                BulkDocumentError.TooLarge,
                $"The file is larger than the {limits.MaxBytes / (1024 * 1024)} MB limit.");
        }

        using var document = new Workbook();
        try
        {
            document.LoadDocument(workbook, DocumentFormat.Xlsx);
        }
        catch (Exception exception) when (exception is not BulkDocumentException)
        {
            throw new BulkDocumentException(
                BulkDocumentError.NotAWorkbook,
                "The file could not be opened as an Excel workbook. Upload the .xlsx template.");
        }

        if (document.Worksheets.Count > limits.MaxSheets)
        {
            throw new BulkDocumentException(
                BulkDocumentError.TooManySheets,
                $"The workbook has more than {limits.MaxSheets} sheets.");
        }

        ReadMetadata(document, descriptor);
        var data = FindDataSheet(document);
        var positions = MapColumns(data, descriptor);
        var rows = ReadRows(data, descriptor, positions, limits);
        return new BulkWorkbookReadResult(descriptor.EntityKey, descriptor.TemplateVersion, rows);
    }

    private static void ReadMetadata(Workbook document, BulkEntityDescriptor descriptor)
    {
        var sheet = document.Worksheets
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                ExcelWorkbookLayout.MetadataSheetName,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new BulkDocumentException(
                BulkDocumentError.MetadataMissing,
                "This workbook was not produced by the template. Download the template and use it.");

        var entityKey = sheet.Cells[0, 1].Value.TextValue?.Trim();
        if (!string.Equals(entityKey, descriptor.EntityKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new BulkDocumentException(
                BulkDocumentError.EntityMismatch,
                $"This is a '{entityKey}' workbook. Upload it on the matching screen, or download the {descriptor.DisplayName} template.");
        }

        var versionText = sheet.Cells[1, 1].Value.TextValue?.Trim();
        if (!int.TryParse(versionText, CultureInfo.InvariantCulture, out var version)
            || version != descriptor.TemplateVersion)
        {
            throw new BulkDocumentException(
                BulkDocumentError.TemplateVersionMismatch,
                $"This workbook uses template version {versionText}; the current version is {descriptor.TemplateVersion}. Download the template again.");
        }
    }

    private static Worksheet FindDataSheet(Workbook document) =>
        document.Worksheets
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                ExcelWorkbookLayout.DataSheetName,
                StringComparison.OrdinalIgnoreCase))
        ?? throw new BulkDocumentException(
            BulkDocumentError.HeaderRowMissing,
            $"The workbook has no '{ExcelWorkbookLayout.DataSheetName}' sheet.");

    /// <summary>
    /// Header text is matched after stripping the required marker and normalising case and spacing,
    /// so an administrator who retyped a header still gets a correct import.
    /// </summary>
    private static Dictionary<string, int> MapColumns(
        Worksheet sheet,
        BulkEntityDescriptor descriptor)
    {
        var used = sheet.GetUsedRange();
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var column = 0; column < used.ColumnCount; column++)
        {
            var text = sheet.Cells[ExcelWorkbookLayout.HeaderRowIndex, column].Value.TextValue;
            var normalised = Normalise(text);
            if (normalised.Length > 0 && !headers.ContainsKey(normalised))
            {
                headers[normalised] = column;
            }
        }

        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var definition in descriptor.Columns)
        {
            if (headers.TryGetValue(Normalise(definition.Header), out var index)
                || headers.TryGetValue(Normalise(definition.ColumnId), out index))
            {
                positions[definition.ColumnId] = index;
            }
            else if (definition.Required)
            {
                missing.Add(definition.Header);
            }
        }

        if (missing.Count > 0)
        {
            throw new BulkDocumentException(
                BulkDocumentError.RequiredColumnMissing,
                $"The workbook is missing these required columns: {string.Join(", ", missing)}.");
        }

        return positions;
    }

    private static List<BulkRow> ReadRows(
        Worksheet sheet,
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, int> positions,
        BulkDocumentLimits limits)
    {
        var used = sheet.GetUsedRange();
        var lastRow = used.TopRowIndex + used.RowCount - 1;
        var rows = new List<BulkRow>();

        for (var row = ExcelWorkbookLayout.FirstDataRowIndex; row <= lastRow; row++)
        {
            var cells = new List<BulkCell>(descriptor.Columns.Count);
            var populated = false;

            foreach (var definition in descriptor.Columns)
            {
                if (!positions.TryGetValue(definition.ColumnId, out var column))
                {
                    cells.Add(new BulkCell(definition.ColumnId, null, null, false));
                    continue;
                }

                var cell = Coerce(definition, sheet.Cells[row, column]);
                populated |= !string.IsNullOrWhiteSpace(cell.SourceText);
                cells.Add(cell);
            }

            /* A blank row inside the used range is a gap left by editing, not a record. */
            if (!populated)
            {
                continue;
            }

            if (rows.Count == limits.MaxRows)
            {
                throw new BulkDocumentException(
                    BulkDocumentError.TooManyRows,
                    $"The workbook has more than {limits.MaxRows} rows. Split it into smaller files.");
            }

            rows.Add(new BulkRow(ExcelWorkbookLayout.ToSourceRowNumber(row), cells));
        }

        return rows;
    }

    /// <summary>
    /// Normalises to invariant text for the declared type. Excel stores a date as a serial number, so
    /// without this the pipeline would see "45934" where the user typed a date.
    /// </summary>
    private static BulkCell Coerce(BulkColumnDefinition definition, Cell cell)
    {
        var value = cell.Value;
        if (value.IsEmpty)
        {
            return new BulkCell(definition.ColumnId, null, null, false);
        }

        var source = value.Type switch
        {
            CellValueType.DateTime => value.DateTimeValue.ToString("O", CultureInfo.InvariantCulture),
            CellValueType.Numeric => value.NumericValue.ToString(CultureInfo.InvariantCulture),
            CellValueType.Boolean => value.BooleanValue ? "true" : "false",
            _ => value.TextValue ?? string.Empty,
        };
        source = source.Trim();

        if (source.Length == 0)
        {
            return new BulkCell(definition.ColumnId, null, null, false);
        }

        return definition.Type switch
        {
            BulkColumnType.Number => CoerceNumber(definition, value, source),
            BulkColumnType.Boolean => CoerceBoolean(definition, value, source),
            BulkColumnType.Date => CoerceDate(definition, value, source),
            _ => new BulkCell(definition.ColumnId, source, source, false),
        };
    }

    private static BulkCell CoerceNumber(
        BulkColumnDefinition definition,
        CellValue value,
        string source)
    {
        if (value.Type == CellValueType.Numeric)
        {
            return new BulkCell(definition.ColumnId, source, source, false);
        }

        return decimal.TryParse(
            source,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var number)
            ? new BulkCell(
                definition.ColumnId,
                number.ToString(CultureInfo.InvariantCulture),
                source,
                false)
            : new BulkCell(definition.ColumnId, null, source, true);
    }

    private static BulkCell CoerceBoolean(
        BulkColumnDefinition definition,
        CellValue value,
        string source)
    {
        if (value.Type == CellValueType.Boolean)
        {
            return new BulkCell(definition.ColumnId, source, source, false);
        }

        var word = source.ToLowerInvariant();
        if (TrueWords.Contains(word))
        {
            return new BulkCell(definition.ColumnId, "true", source, false);
        }

        return FalseWords.Contains(word)
            ? new BulkCell(definition.ColumnId, "false", source, false)
            : new BulkCell(definition.ColumnId, null, source, true);
    }

    private static BulkCell CoerceDate(
        BulkColumnDefinition definition,
        CellValue value,
        string source)
    {
        if (value.Type == CellValueType.DateTime)
        {
            return new BulkCell(definition.ColumnId, source, source, false);
        }

        return DateTime.TryParse(
            source,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var moment)
            ? new BulkCell(
                definition.ColumnId,
                moment.ToString("O", CultureInfo.InvariantCulture),
                source,
                false)
            : new BulkCell(definition.ColumnId, null, source, true);
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
