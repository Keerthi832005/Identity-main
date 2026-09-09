using Identity.Application.BulkData;

namespace Identity.Application.Administration;

public sealed class UserRolesBulkValidator(
    IUserCodeResolver users,
    ICatalogCodeResolver catalog,
    IAccessGrantLookup grants) : AccessGrantBulkValidator(users, catalog)
{
    public override string EntityKey => AccessGrantBulkDescriptors.UserRolesKey;

    /// <summary>
    /// Role codes are unique per application, so a role that exists elsewhere but not here is the
    /// likely mistake: the message says so rather than just reporting it as unknown.
    /// </summary>
    protected override void CheckRole(
        BulkRow row,
        IReadOnlyDictionary<string, long> roles,
        List<BulkCellError> errors)
    {
        var roleCode = Value(row, AccessGrantBulkDescriptors.RoleCode);
        if (roleCode is not null && !roles.ContainsKey(roleCode))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                AccessGrantBulkDescriptors.RoleCode,
                UnknownRole,
                $"No role {roleCode} in this application. A role belongs to one application; check the application code too."));
        }
    }

    protected override async ValueTask<bool> Exists(
        long userId,
        long applicationId,
        BulkRow row,
        IReadOnlyDictionary<string, long> roles,
        CancellationToken cancellationToken)
    {
        var roleCode = Value(row, AccessGrantBulkDescriptors.RoleCode);
        if (userId <= 0 || applicationId <= 0
            || roleCode is null || !roles.TryGetValue(roleCode, out var roleId))
        {
            return false;
        }

        return await grants
            .HasRoleAssignment(userId, applicationId, roleId, cancellationToken)
            .ConfigureAwait(false);
    }
}
