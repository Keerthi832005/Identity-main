using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Shared validation for application grants and role assignments. Users, applications and roles are
/// each resolved once for the whole batch, so a file of any size costs a fixed number of queries.
/// </summary>
public abstract class AccessGrantBulkValidator(
    IUserCodeResolver users,
    ICatalogCodeResolver catalog) : IBulkEntityValidator
{
    public const string UnknownUser = "user.unknown";
    public const string UnknownApplication = "application.unknown";
    public const string UnknownRole = "role.unknown";

    public abstract string EntityKey { get; }

    /// <summary>Whether this row already matches a persisted grant, so it commits as an update.</summary>
    protected abstract ValueTask<bool> Exists(
        long userId,
        long applicationId,
        BulkRow row,
        IReadOnlyDictionary<string, long> roles,
        CancellationToken cancellationToken);

    protected virtual void CheckRole(
        BulkRow row,
        IReadOnlyDictionary<string, long> roles,
        List<BulkCellError> errors)
    {
    }

    public async ValueTask<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(rows);

        var knownUsers = await users
            .FindUserIdsByEmployeeCodes(Codes(rows, AccessGrantBulkDescriptors.EmployeeCode), cancellationToken)
            .ConfigureAwait(false);
        var knownApplications = await catalog
            .FindApplicationIdsByCodes(Codes(rows, AccessGrantBulkDescriptors.ApplicationCode), cancellationToken)
            .ConfigureAwait(false);

        /* Role codes are unique per application, so they are resolved per application rather than
           globally; a role code may legitimately repeat across applications. */
        var rolesByApplication = new Dictionary<long, IReadOnlyDictionary<string, long>>();
        foreach (var applicationId in knownApplications.Values.Distinct())
        {
            rolesByApplication[applicationId] = await catalog
                .FindRoleIdsByCodes(
                    applicationId,
                    Codes(rows, AccessGrantBulkDescriptors.RoleCode),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var verdicts = new List<BulkEntityRowVerdict>(rows.Count);
        foreach (var row in rows)
        {
            var errors = new List<BulkCellError>();
            var employeeCode = Value(row, AccessGrantBulkDescriptors.EmployeeCode);
            var applicationCode = Value(row, AccessGrantBulkDescriptors.ApplicationCode);

            long userId = 0;
            long applicationId = 0;

            if (employeeCode is not null && !knownUsers.TryGetValue(employeeCode, out userId))
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    AccessGrantBulkDescriptors.EmployeeCode,
                    UnknownUser,
                    $"No user with employee code {employeeCode}. Import the users file first."));
            }

            if (applicationCode is not null
                && !knownApplications.TryGetValue(applicationCode, out applicationId))
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    AccessGrantBulkDescriptors.ApplicationCode,
                    UnknownApplication,
                    $"No application with code {applicationCode}."));
            }

            var roles = applicationId > 0 && rolesByApplication.TryGetValue(applicationId, out var found)
                ? found
                : new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (errors.Count == 0)
            {
                CheckRole(row, roles, errors);
            }

            var matches = errors.Count == 0
                && await Exists(userId, applicationId, row, roles, cancellationToken)
                    .ConfigureAwait(false);
            verdicts.Add(new BulkEntityRowVerdict(row.SourceRowNumber, matches, errors));
        }

        return verdicts;
    }

    protected static string[] Codes(IReadOnlyList<BulkRow> rows, string columnId) =>
        [.. rows.Select(row => Value(row, columnId)).Where(code => code is not null).Select(code => code!)];

    protected static string? Value(BulkRow row, string columnId) =>
        row.Cells.FirstOrDefault(cell => cell.ColumnId == columnId)?.Value is { } value
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
