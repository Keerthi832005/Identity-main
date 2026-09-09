using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// One staged row. Values and errors are held as serialized payloads: the shape belongs to the
/// template descriptor, which the domain deliberately does not know about.
/// </summary>
public sealed class BulkImportRow
{
    private BulkImportRow() { }

    public static BulkImportRow Stage(
        long batchId,
        int sourceRowNumber,
        string cellValues,
        string? cellErrors,
        BulkImportRowState state)
    {
        var row = new BulkImportRow
        {
            BulkImportBatchId = DomainRules.Positive(batchId, nameof(batchId)),
            SourceRowNumber = (int)DomainRules.Positive(sourceRowNumber, nameof(sourceRowNumber)),
        };
        row.Apply(cellValues, cellErrors, state);
        return row;
    }

    public void Correct(
        string cellValues,
        string? cellErrors,
        BulkImportRowState state,
        DateTime updatedAt)
    {
        if (State == BulkImportRowState.Applied)
        {
            throw new InvalidOperationException("An applied row cannot be corrected.");
        }

        Apply(cellValues, cellErrors, state);
        UpdatedAt = updatedAt;
    }

    public void MarkApplied(long? resourceId, DateTime appliedAt)
    {
        if (State is not (BulkImportRowState.Create or BulkImportRowState.Update))
        {
            throw new InvalidOperationException("Only a valid staged row can be applied.");
        }

        State = BulkImportRowState.Applied;
        AppliedResourceId = resourceId;
        CellErrors = null;
        UpdatedAt = appliedAt;
    }

    public bool IsReady => State is BulkImportRowState.Create or BulkImportRowState.Update;

    private void Apply(string cellValues, string? cellErrors, BulkImportRowState state)
    {
        if (string.IsNullOrWhiteSpace(cellValues))
        {
            throw new ArgumentException("Cell values are required.", nameof(cellValues));
        }

        if (state == BulkImportRowState.Applied)
        {
            throw new ArgumentException("Use MarkApplied to apply a row.", nameof(state));
        }

        /* The database enforces the same pairing; keeping it here stops an invalid row reaching SQL
           with no explanation attached, and a valid row carrying a stale one. */
        if (state == BulkImportRowState.Invalid == string.IsNullOrWhiteSpace(cellErrors))
        {
            throw new ArgumentException(
                "An invalid row must carry errors, and a valid row must not.",
                nameof(cellErrors));
        }

        CellValues = cellValues;
        CellErrors = state == BulkImportRowState.Invalid ? cellErrors : null;
        State = state;
    }

    public long BulkImportRowId { get; private set; }
    public long BulkImportBatchId { get; private set; }
    public int SourceRowNumber { get; private set; }
    public string CellValues { get; private set; } = string.Empty;
    public string? CellErrors { get; private set; }
    public BulkImportRowState State { get; private set; }
    public long? AppliedResourceId { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
