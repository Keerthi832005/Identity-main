using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

/// <summary>
/// Descriptor-driven validation, applied identically to an Excel upload and a smart paste. Pure and
/// synchronous: the caller fetches entity verdicts in one batched pass and hands them in, so this
/// stays fully testable and cannot issue a query per row.
/// </summary>
public sealed class BulkValidationPipeline
{
    public IReadOnlyList<BulkRowOutcome> Validate(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        IReadOnlyList<BulkEntityRowVerdict> verdicts)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(verdicts);

        var byRow = verdicts.ToDictionary(verdict => verdict.SourceRowNumber);
        var duplicates = FindDuplicateKeys(descriptor, rows);
        var outcomes = new List<BulkRowOutcome>(rows.Count);

        foreach (var row in rows)
        {
            var errors = new List<BulkCellError>();
            foreach (var definition in descriptor.Columns)
            {
                CheckCell(definition, row, errors);
            }

            if (duplicates.TryGetValue(row.SourceRowNumber, out var firstRow))
            {
                foreach (var columnId in KeyColumns(descriptor))
                {
                    errors.Add(new BulkCellError(
                        row.SourceRowNumber,
                        columnId,
                        BulkErrorCodes.DuplicateInBatch,
                        $"Row {firstRow} already uses this value. It must be unique within the file."));
                }
            }

            var matchesExisting = false;
            if (byRow.TryGetValue(row.SourceRowNumber, out var verdict))
            {
                errors.AddRange(verdict.Errors);
                matchesExisting = verdict.MatchesExistingRecord;
            }

            var state = errors.Count > 0
                ? BulkImportRowState.Invalid
                : matchesExisting ? BulkImportRowState.Update : BulkImportRowState.Create;
            outcomes.Add(new BulkRowOutcome(row.SourceRowNumber, state, errors));
        }

        return outcomes;
    }

    private static void CheckCell(
        BulkColumnDefinition definition,
        BulkRow row,
        List<BulkCellError> errors)
    {
        var cell = row.Cells.FirstOrDefault(candidate => candidate.ColumnId == definition.ColumnId);
        if (cell is null)
        {
            if (definition.Required)
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    definition.ColumnId,
                    BulkErrorCodes.Required,
                    $"{definition.Header} is required but the column is missing."));
            }

            return;
        }

        if (cell.Malformed)
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                definition.ColumnId,
                BulkErrorCodes.Malformed,
                Describe(definition, cell.SourceText)));
            return;
        }

        if (string.IsNullOrWhiteSpace(cell.Value))
        {
            if (definition.Required)
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    definition.ColumnId,
                    BulkErrorCodes.Required,
                    $"{definition.Header} is required. This cell is empty."));
            }

            return;
        }

        if (definition.MaxLength is { } limit && cell.Value.Length > limit)
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                definition.ColumnId,
                BulkErrorCodes.TooLong,
                $"{definition.Header} is limited to {limit} characters; this value has {cell.Value.Length}."));
            return;
        }

        /* A boolean column's dropdown labels (Yes/No) are not its stored values, which the reader has
           already normalised to true/false. Checking the label list here would reject every row. */
        if (definition.Type is not BulkColumnType.Boolean
            && definition.AllowedValues is { Count: > 0 } allowed
            && !allowed.Contains(cell.Value, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                definition.ColumnId,
                BulkErrorCodes.NotAllowed,
                $"'{cell.SourceText}' is not a valid {definition.Header}. Allowed values: {string.Join(", ", allowed)}."));
        }

    }

    private static string Describe(BulkColumnDefinition definition, string? source) =>
        definition.Type switch
        {
            BulkColumnType.Number =>
                $"'{source}' is not a number. Enter digits only, without units or symbols.",
            BulkColumnType.Date =>
                $"'{source}' is not a date. Use a real date cell, or type it as 2026-08-31.",
            BulkColumnType.Boolean =>
                $"'{source}' is not a yes or no value. Use Yes or No.",
            _ => $"'{source}' is not a valid {definition.Header}.",
        };

    /// <summary>Maps each duplicate row to the earlier row that already claimed its key.</summary>
    private static Dictionary<int, int> FindDuplicateKeys(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows)
    {
        var keyColumns = KeyColumns(descriptor);
        var duplicates = new Dictionary<int, int>();
        if (keyColumns.Count == 0)
        {
            return duplicates;
        }

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var parts = keyColumns
                .Select(columnId => row.Cells
                    .FirstOrDefault(cell => cell.ColumnId == columnId)?.Value)
                .ToArray();

            /* An incomplete key is reported as a missing required value, not as a duplicate. */
            if (parts.Any(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            /* Unit separator: a value containing it is not reachable from a spreadsheet cell, so two
               different key tuples can never collide into one string. */
            var key = string.Join('\u001F', parts);
            if (seen.TryGetValue(key, out var firstRow))
            {
                duplicates[row.SourceRowNumber] = firstRow;
            }
            else
            {
                seen[key] = row.SourceRowNumber;
            }
        }

        return duplicates;
    }

    private static IReadOnlyList<string> KeyColumns(BulkEntityDescriptor descriptor) =>
        descriptor.KeyColumnIds ?? [];
}
