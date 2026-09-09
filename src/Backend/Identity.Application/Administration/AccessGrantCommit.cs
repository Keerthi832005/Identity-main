namespace Identity.Application.Administration;

/// <summary>Resolves the natural codes a staged access-grant row carries into ids at apply time.</summary>
internal static class AccessGrantCommit
{
    internal static async Task<(long UserId, long ApplicationId)> Resolve(
        IUserCodeResolver users,
        ICatalogCodeResolver catalog,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        var employeeCode = Required(values, AccessGrantBulkDescriptors.EmployeeCode);
        var applicationCode = Required(values, AccessGrantBulkDescriptors.ApplicationCode);

        var matchedUsers = await users
            .FindUserIdsByEmployeeCodes([employeeCode], cancellationToken)
            .ConfigureAwait(false);
        if (!matchedUsers.TryGetValue(employeeCode, out var userId))
        {
            throw new AdministrationException($"User {employeeCode} no longer exists.");
        }

        var matchedApplications = await catalog
            .FindApplicationIdsByCodes([applicationCode], cancellationToken)
            .ConfigureAwait(false);
        if (!matchedApplications.TryGetValue(applicationCode, out var applicationId))
        {
            throw new AdministrationException($"Application {applicationCode} no longer exists.");
        }

        return (userId, applicationId);
    }

    internal static string Required(IReadOnlyDictionary<string, string?> values, string columnId) =>
        values.TryGetValue(columnId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new AdministrationException($"Staged row is missing {columnId}.");
}
