using Identity.Application.BulkData;

namespace Identity.Application.Administration;

public sealed class UserApplicationsBulkValidator(
    IUserCodeResolver users,
    ICatalogCodeResolver catalog,
    IAccessGrantLookup grants) : AccessGrantBulkValidator(users, catalog)
{
    public override string EntityKey => AccessGrantBulkDescriptors.UserApplicationsKey;

    protected override async ValueTask<bool> Exists(
        long userId,
        long applicationId,
        BulkRow row,
        IReadOnlyDictionary<string, long> roles,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || applicationId <= 0)
        {
            return false;
        }

        return await grants
            .HasApplicationGrant(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
    }
}
