using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

/// <summary>
/// Applies one staged user row by dispatching the same command a single-record write uses, so a bulk
/// import inherits the identical domain rules, authorization and audit rather than a parallel path.
/// </summary>
public sealed class UsersBulkCommitter(
    IRequestDispatcher dispatcher,
    IUserCodeResolver resolver,
    IOrganizationCodeResolver organizations,
    IAdministrationStore store) : IBulkEntityCommitter
{
    public string EntityKey => UsersBulkDescriptor.EntityKey;

    public async ValueTask<long?> Apply(
        BulkEntityDescriptor descriptor,
        IReadOnlyDictionary<string, string?> values,
        bool isUpdate,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(values);

        var employeeCode = Required(values, UsersBulkDescriptor.EmployeeCode);
        var displayName = Required(values, UsersBulkDescriptor.DisplayName);
        var email = Optional(values, UsersBulkDescriptor.Email);
        var managerCode = Optional(values, UsersBulkDescriptor.ManagerEmployeeCode);
        var branchCode = Optional(values, UsersBulkDescriptor.BranchCode);

        /* Resolved at apply time, not at validation time: a manager created earlier in this same
           commit does not exist until its own row has been applied. */
        long? managerUserId = null;
        if (managerCode is not null)
        {
            var resolved = await resolver
                .FindUserIdsByEmployeeCodes([managerCode], cancellationToken)
                .ConfigureAwait(false);
            managerUserId = resolved.TryGetValue(managerCode, out var id) ? id : null;
        }

        if (!isUpdate)
        {
            var branchId = await ResolveBranch(branchCode, cancellationToken);
            var created = await dispatcher.Send(
                new CreateUserCommand(employeeCode, displayName, context, email, managerUserId,
                    branchId.HasValue ? new UserOrganizationMapping(null, null, branchId) : null),
                cancellationToken);
            return created.ResourceId;
        }

        var existing = await resolver
            .FindUserIdsByEmployeeCodes([employeeCode], cancellationToken)
            .ConfigureAwait(false);
        if (!existing.TryGetValue(employeeCode, out var userId))
        {
            throw new AdministrationException(
                $"User {employeeCode} was staged as an update but no longer exists.");
        }

        UserOrganizationMapping? mapping = null;
        if (branchCode is not null)
        {
            var user = await store.FindUser(userId, cancellationToken)
                ?? throw new AdministrationException($"User {employeeCode} no longer exists.");
            mapping = new UserOrganizationMapping(user.DepartmentId, user.TeamId,
                await ResolveBranch(branchCode, cancellationToken));
        }
        var updated = await dispatcher.Send(
            new UpdateUserProfileCommand(userId, displayName, email, managerUserId, context, mapping),
            cancellationToken);
        return updated.ResourceId;
    }

    private static string Required(IReadOnlyDictionary<string, string?> values, string columnId) =>
        values.TryGetValue(columnId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new AdministrationException($"Staged row is missing {columnId}.");

    private static string? Optional(IReadOnlyDictionary<string, string?> values, string columnId) =>
        values.TryGetValue(columnId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private async Task<long?> ResolveBranch(string? branchCode, CancellationToken cancellationToken)
    {
        if (branchCode is null) return null;
        var matches = await organizations.FindUnitsByCodes([branchCode], cancellationToken).ConfigureAwait(false);
        if (!matches.TryGetValue(branchCode, out var branch)
            || branch.UnitType != Identity.Domain.Enums.OrganizationUnitType.Branch)
            throw new AdministrationException($"Branch {branchCode} was not found.");
        return branch.OrganizationUnitId;
    }
}
