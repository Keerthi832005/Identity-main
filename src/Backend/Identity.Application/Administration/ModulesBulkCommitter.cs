using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

/// <summary>
/// Creates modules through the existing command. Parents are applied before children regardless of
/// where they sit in the file, so one workbook can define a whole module tree.
/// </summary>
public sealed class ModulesBulkCommitter(
    IRequestDispatcher dispatcher,
    ICatalogCodeResolver resolver) : IBulkEntityCommitter
{
    public string EntityKey => CatalogBulkDescriptors.ModulesKey;

    /// <summary>
    /// Depth order. A row whose parent is created by another row in this batch is applied after it;
    /// a row whose parent already exists, or has none, can go first.
    /// </summary>
    public IReadOnlyList<int> OrderForCommit(IReadOnlyList<BulkCommitCandidate> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var byCode = new Dictionary<string, BulkCommitCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (CatalogCommit.Optional(row.Values, CatalogBulkDescriptors.ModuleCode) is { } code)
            {
                byCode.TryAdd(code, row);
            }
        }

        var ordered = new List<int>(rows.Count);
        var placed = new HashSet<int>();
        var walking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Place(BulkCommitCandidate row)
        {
            if (!placed.Add(row.SourceRowNumber))
            {
                return;
            }

            var code = CatalogCommit.Optional(row.Values, CatalogBulkDescriptors.ModuleCode);
            var parent = CatalogCommit.Optional(row.Values, CatalogBulkDescriptors.ParentModuleCode);

            /* The walking set guards against a cycle the validator did not reject; without it this
               would recurse until the stack ran out rather than fail one commit. */
            if (parent is not null
                && code is not null
                && walking.Add(code)
                && byCode.TryGetValue(parent, out var ancestor))
            {
                Place(ancestor);
            }

            ordered.Add(row.SourceRowNumber);
        }

        foreach (var row in rows)
        {
            Place(row);
        }

        return ordered;
    }

    public async ValueTask<long?> Apply(
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values,
        bool isUpdate,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var applicationId = await CatalogCommit
            .ApplicationId(resolver, values, cancellationToken)
            .ConfigureAwait(false);

        long? parentId = null;
        if (CatalogCommit.Optional(values, CatalogBulkDescriptors.ParentModuleCode) is { } parentCode)
        {
            /* Resolved at apply time: a parent created earlier in this same commit did not exist
               when the batch was validated. */
            var modules = await resolver
                .FindModuleIdsByCodes(applicationId, [parentCode], cancellationToken)
                .ConfigureAwait(false);
            parentId = modules.TryGetValue(parentCode, out var id)
                ? id
                : throw new AdministrationException(
                    $"Parent module {parentCode} was not created before its children.");
        }

        var result = await dispatcher.Send(
            new CreateModuleCommand(
                applicationId,
                CatalogCommit.Required(values, CatalogBulkDescriptors.ModuleCode),
                CatalogCommit.Required(values, CatalogBulkDescriptors.Name),
                CatalogCommit.Optional(values, CatalogBulkDescriptors.Description),
                parentId,
                CatalogCommit.Number(values, CatalogBulkDescriptors.DisplayOrder),
                CatalogCommit.Flag(values, CatalogBulkDescriptors.IsSystem),
                context),
            cancellationToken);
        return result.ResourceId;
    }
}
