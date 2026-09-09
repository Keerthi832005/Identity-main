using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Shared entity validation for modules, capabilities and roles. Every reference is a natural code,
/// resolved for the whole batch in a fixed number of queries.
/// </summary>
public abstract class CatalogBulkValidator(ICatalogCodeResolver resolver) : IBulkEntityValidator
{
    public const string UnknownApplication = "application.unknown";
    public const string UnknownModule = "module.unknown";
    public const string UnknownParentModule = "module.parent-unknown";
    public const string ParentIsSelf = "module.parent-self";
    public const string ParentCycle = "module.parent-cycle";

    public abstract string EntityKey { get; }

    protected abstract ValueTask Check(
        CatalogBatch batch,
        BulkRow row,
        long applicationId,
        List<BulkCellError> errors,
        CancellationToken cancellationToken);

    /// <summary>Whether a row already matches a persisted record, so it commits as an update.</summary>
    protected abstract bool Exists(CatalogBatch batch, BulkRow row, long applicationId);

    public async ValueTask<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(rows);

        var applicationCodes = rows
            .Select(row => Value(row, CatalogBulkDescriptors.ApplicationCode))
            .Where(code => code is not null)
            .Select(code => code!)
            .ToArray();
        var applications = await resolver
            .FindApplicationIdsByCodes(applicationCodes, cancellationToken)
            .ConfigureAwait(false);

        var batch = await Load(rows, applications, cancellationToken).ConfigureAwait(false);
        var verdicts = new List<BulkEntityRowVerdict>(rows.Count);

        foreach (var row in rows)
        {
            var errors = new List<BulkCellError>();
            var applicationCode = Value(row, CatalogBulkDescriptors.ApplicationCode);
            if (applicationCode is null || !applications.TryGetValue(applicationCode, out var applicationId))
            {
                if (applicationCode is not null)
                {
                    errors.Add(new BulkCellError(
                        row.SourceRowNumber,
                        CatalogBulkDescriptors.ApplicationCode,
                        UnknownApplication,
                        $"No application with code {applicationCode}. Create the application first."));
                }

                verdicts.Add(new BulkEntityRowVerdict(row.SourceRowNumber, false, errors));
                continue;
            }

            await Check(batch, row, applicationId, errors, cancellationToken).ConfigureAwait(false);
            verdicts.Add(new BulkEntityRowVerdict(
                row.SourceRowNumber,
                errors.Count == 0 && Exists(batch, row, applicationId),
                errors));
        }

        return verdicts;
    }

    private async Task<CatalogBatch> Load(
        IReadOnlyList<BulkRow> rows,
        IReadOnlyDictionary<string, long> applications,
        CancellationToken cancellationToken)
    {
        var modules = new Dictionary<long, IReadOnlyDictionary<string, long>>();
        var roles = new Dictionary<long, IReadOnlyDictionary<string, long>>();
        var capabilities = new Dictionary<long, IReadOnlyDictionary<string, long>>();

        foreach (var applicationId in applications.Values.Distinct())
        {
            var moduleCodes = Codes(rows, CatalogBulkDescriptors.ModuleCode)
                .Concat(Codes(rows, CatalogBulkDescriptors.ParentModuleCode))
                .ToArray();
            modules[applicationId] = await resolver
                .FindModuleIdsByCodes(applicationId, moduleCodes, cancellationToken)
                .ConfigureAwait(false);
            roles[applicationId] = await resolver
                .FindRoleIdsByCodes(applicationId, Codes(rows, CatalogBulkDescriptors.RoleCode), cancellationToken)
                .ConfigureAwait(false);
            capabilities[applicationId] = await resolver
                .FindCapabilityIdsByCodes(
                    applicationId,
                    Codes(rows, CatalogBulkDescriptors.CapabilityCode),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new CatalogBatch(rows, applications, modules, roles, capabilities);
    }

    private static string[] Codes(IReadOnlyList<BulkRow> rows, string columnId) =>
        [.. rows.Select(row => Value(row, columnId)).Where(code => code is not null).Select(code => code!)];

    protected static string? Value(BulkRow row, string columnId) =>
        row.Cells.FirstOrDefault(cell => cell.ColumnId == columnId)?.Value is { } value
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
