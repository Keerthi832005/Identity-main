using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Modules form a tree, so a parent may legitimately be defined by another row in the same file.
/// Parent references are checked against the database and the batch together, and a cycle inside the
/// batch is rejected before anything is staged.
/// </summary>
public sealed class ModulesBulkValidator(ICatalogCodeResolver resolver)
    : CatalogBulkValidator(resolver)
{
    public override string EntityKey => CatalogBulkDescriptors.ModulesKey;

    protected override bool Exists(CatalogBatch batch, BulkRow row, long applicationId) =>
        Value(row, CatalogBulkDescriptors.ModuleCode) is { } code
        && batch.ModuleExists(applicationId, code);

    protected override ValueTask Check(
        CatalogBatch batch,
        BulkRow row,
        long applicationId,
        List<BulkCellError> errors,
        CancellationToken cancellationToken)
    {
        var code = Value(row, CatalogBulkDescriptors.ModuleCode);
        var parent = Value(row, CatalogBulkDescriptors.ParentModuleCode);
        if (code is null || parent is null)
        {
            return ValueTask.CompletedTask;
        }

        if (string.Equals(code, parent, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                CatalogBulkDescriptors.ParentModuleCode,
                ParentIsSelf,
                "A module cannot be its own parent."));
            return ValueTask.CompletedTask;
        }

        var inBatch = ParentsInBatch(batch);
        if (!batch.ModuleExists(applicationId, parent) && !inBatch.ContainsKey(parent))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                CatalogBulkDescriptors.ParentModuleCode,
                UnknownParentModule,
                $"No module with code {parent}. Add it as another row in this file, or correct the code."));
            return ValueTask.CompletedTask;
        }

        if (FormsCycle(code, inBatch))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                CatalogBulkDescriptors.ParentModuleCode,
                ParentCycle,
                "These modules reference each other in a loop. A module tree cannot contain a cycle."));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Child code to parent code, for every row in the batch that names a parent.</summary>
    private static Dictionary<string, string> ParentsInBatch(CatalogBatch batch)
    {
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch.Rows)
        {
            var code = Value(row, CatalogBulkDescriptors.ModuleCode);
            if (code is null || parents.ContainsKey(code)) continue;
            parents[code] = Value(row, CatalogBulkDescriptors.ParentModuleCode) ?? string.Empty;
        }

        return parents;
    }

    private static bool FormsCycle(string code, IReadOnlyDictionary<string, string> parents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = code;
        while (parents.TryGetValue(current, out var parent) && parent.Length > 0)
        {
            if (!seen.Add(current)) return true;
            if (string.Equals(parent, code, StringComparison.OrdinalIgnoreCase)) return true;
            current = parent;
        }

        return false;
    }
}
