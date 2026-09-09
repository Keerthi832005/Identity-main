using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

/// <summary>
/// Grants application access through the existing command, so a bulk grant carries the same
/// authorization, authorization-version bump and audit as a single-record grant.
/// </summary>
public sealed class UserApplicationsBulkCommitter(
    IRequestDispatcher dispatcher,
    IUserCodeResolver users,
    ICatalogCodeResolver catalog) : IBulkEntityCommitter
{
    public string EntityKey => AccessGrantBulkDescriptors.UserApplicationsKey;

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

        var result = await dispatcher.Send(
            new GrantUserApplicationCommand(userId, applicationId, context),
            cancellationToken);
        return result.ResourceId;
    }
}
