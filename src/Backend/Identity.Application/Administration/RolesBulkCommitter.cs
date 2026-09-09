using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed class RolesBulkCommitter(
    IRequestDispatcher dispatcher,
    ICatalogCodeResolver resolver) : IBulkEntityCommitter
{
    public string EntityKey => CatalogBulkDescriptors.RolesKey;

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

        var result = await dispatcher.Send(
            new CreateRoleCommand(
                applicationId,
                CatalogCommit.Required(values, CatalogBulkDescriptors.RoleCode),
                CatalogCommit.Required(values, CatalogBulkDescriptors.Name),
                CatalogCommit.Optional(values, CatalogBulkDescriptors.Description),
                CatalogCommit.Flag(values, CatalogBulkDescriptors.IsSystem),
                context),
            cancellationToken);
        return result.ResourceId;
    }
}
