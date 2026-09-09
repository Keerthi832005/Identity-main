using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed class UserRolesBulkCommitter(
    IRequestDispatcher dispatcher,
    IUserCodeResolver users,
    ICatalogCodeResolver catalog) : IBulkEntityCommitter
{
    public string EntityKey => AccessGrantBulkDescriptors.UserRolesKey;

    public async ValueTask<long?> Apply(
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values,
        bool isUpdate,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var (userId, applicationId) = await AccessGrantCommit
            .Resolve(users, catalog, values, cancellationToken)
            .ConfigureAwait(false);

        var roleCode = AccessGrantCommit.Required(values, AccessGrantBulkDescriptors.RoleCode);
        var roles = await catalog
            .FindRoleIdsByCodes(applicationId, [roleCode], cancellationToken)
            .ConfigureAwait(false);
        if (!roles.TryGetValue(roleCode, out var roleId))
        {
            throw new AdministrationException($"Role {roleCode} no longer exists in this application.");
        }

        var result = await dispatcher.Send(
            new AssignRoleCommand(userId, applicationId, roleId, context),
            cancellationToken);
        return result.ResourceId;
    }
}
