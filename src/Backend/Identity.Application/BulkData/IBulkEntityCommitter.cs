using Identity.Application.Administration;

namespace Identity.Application.BulkData;

/// <summary>
/// Applies one validated staged row. Implementations project the row through the existing
/// administration commands so a bulk write reuses the same domain rules, authorization and audit as
/// a single-record write; they never touch the stores directly.
/// </summary>
public interface IBulkEntityCommitter
{
    string EntityKey { get; }

    ValueTask<long?> Apply(
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values,
        bool isUpdate,
        AdministrationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Order in which rows are applied, by source row number. The default is file order, which is
    /// right for a flat entity; a hierarchy overrides it so a parent is created before its children
    /// no matter where the administrator put them in the file.
    /// </summary>
    IReadOnlyList<int> OrderForCommit(IReadOnlyList<BulkCommitCandidate> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Select(row => row.SourceRowNumber)];
    }
}
