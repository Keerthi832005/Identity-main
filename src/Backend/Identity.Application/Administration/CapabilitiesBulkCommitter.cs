using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed class CapabilitiesBulkCommitter(
    IRequestDispatcher dispatcher,
    ICatalogCodeResolver resolver) : IBulkEntityCommitter
{
    public string EntityKey => CatalogBulkDescriptors.CapabilitiesKey;

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
        var moduleCode = CatalogCommit.Required(values, CatalogBulkDescriptors.ModuleCode);
        var modules = await resolver
            .FindModuleIdsByCodes(applicationId, [moduleCode], cancellationToken)
            .ConfigureAwait(false);
        if (!modules.TryGetValue(moduleCode, out var moduleId))
        {
            throw new AdministrationException($"Module {moduleCode} no longer exists.");
        }

        var result = await dispatcher.Send(
            new CreateCapabilityCommand(
                applicationId,
                moduleId,
                CatalogCommit.Required(values, CatalogBulkDescriptors.CapabilityCode),
                CatalogCommit.Required(values, CatalogBulkDescriptors.Name),
                CatalogCommit.Optional(values, CatalogBulkDescriptors.Description),
                context),
            cancellationToken);
        return result.ResourceId;
    }
}
