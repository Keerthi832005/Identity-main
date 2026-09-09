namespace Identity.Application.BulkData;

/// <summary>
/// Entity-specific checks: lookups by natural key, hierarchy rules and existing-record detection.
/// Implementations verify the whole batch in one pass, never one query per row.
/// </summary>
public interface IBulkEntityValidator
{
    string EntityKey { get; }

    ValueTask<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        CancellationToken cancellationToken);
}
