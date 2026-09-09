using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Entity checks the template alone cannot make: does this employee code already exist, and does the
/// named manager resolve? Both are answered from a single lookup for the whole batch.
/// </summary>
public sealed class UsersBulkValidator(IUserCodeResolver resolver, IOrganizationCodeResolver organizations) : IBulkEntityValidator
{
    public const string UnknownManager = "manager.unknown";
    public const string ManagerIsSelf = "manager.self";
    public const string EmailInvalid = "email.invalid";
    public const string UnknownBranch = "branch.unknown";
    public const string WrongBranchType = "branch.wrong-type";

    public string EntityKey => UsersBulkDescriptor.EntityKey;

    public async ValueTask<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        BulkEntityDescriptor descriptor,
        IReadOnlyList<BulkRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(rows);

        var codes = rows
            .SelectMany(row => new[]
            {
                Value(row, UsersBulkDescriptor.EmployeeCode),
                Value(row, UsersBulkDescriptor.ManagerEmployeeCode),
            })
            .Where(code => code is not null)
            .Select(code => code!)
            .ToArray();
        var existing = await resolver
            .FindUserIdsByEmployeeCodes(codes, cancellationToken)
            .ConfigureAwait(false);
        var branchCodes = rows.Select(row => Value(row, UsersBulkDescriptor.BranchCode))
            .Where(code => code is not null).Select(code => code!).ToArray();
        var branches = await organizations.FindUnitsByCodes(branchCodes, cancellationToken).ConfigureAwait(false);

        /* A manager created earlier in the same file is a legitimate reference, so in-batch codes
           count as resolvable even though they are not yet persisted. */
        var inBatch = rows
            .Select(row => Value(row, UsersBulkDescriptor.EmployeeCode))
            .Where(code => code is not null)
            .Select(code => code!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var verdicts = new List<BulkEntityRowVerdict>(rows.Count);
        foreach (var row in rows)
        {
            var errors = new List<BulkCellError>();
            var employeeCode = Value(row, UsersBulkDescriptor.EmployeeCode);
            var manager = Value(row, UsersBulkDescriptor.ManagerEmployeeCode);
            var email = Value(row, UsersBulkDescriptor.Email);
            var branchCode = Value(row, UsersBulkDescriptor.BranchCode);

            if (manager is not null)
            {
                if (employeeCode is not null
                    && string.Equals(manager, employeeCode, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new BulkCellError(
                        row.SourceRowNumber,
                        UsersBulkDescriptor.ManagerEmployeeCode,
                        ManagerIsSelf,
                        "A user cannot be their own reporting manager."));
                }
                else if (!existing.ContainsKey(manager) && !inBatch.Contains(manager))
                {
                    errors.Add(new BulkCellError(
                        row.SourceRowNumber,
                        UsersBulkDescriptor.ManagerEmployeeCode,
                        UnknownManager,
                        $"No user with employee code {manager}. Add that manager as a row above this one, or correct the code."));
                }
            }

            if (email is not null && !IsPlausibleEmail(email))
            {
                errors.Add(new BulkCellError(
                    row.SourceRowNumber,
                    UsersBulkDescriptor.Email,
                    EmailInvalid,
                    $"'{email}' is not a valid email address."));
            }

            if (branchCode is not null)
            {
                if (!branches.TryGetValue(branchCode, out var branch))
                {
                    errors.Add(new BulkCellError(row.SourceRowNumber, UsersBulkDescriptor.BranchCode,
                        UnknownBranch, $"No branch with code {branchCode}."));
                }
                else if (branch.UnitType != Identity.Domain.Enums.OrganizationUnitType.Branch)
                {
                    errors.Add(new BulkCellError(row.SourceRowNumber, UsersBulkDescriptor.BranchCode,
                        WrongBranchType, $"{branchCode} is not a branch."));
                }
            }

            verdicts.Add(new BulkEntityRowVerdict(
                row.SourceRowNumber,
                employeeCode is not null && existing.ContainsKey(employeeCode),
                errors));
        }

        return verdicts;
    }

    private static string? Value(BulkRow row, string columnId) =>
        row.Cells.FirstOrDefault(cell => cell.ColumnId == columnId)?.Value is { } value
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    /* Deliberately shallow. The domain owns the authoritative rule; this only catches the obvious
       typo early enough to show it against the right cell. */
    private static bool IsPlausibleEmail(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@')) return false;
        var domain = value[(at + 1)..];
        return domain.Length >= 3
            && domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !value.Contains(' ', StringComparison.Ordinal);
    }
}
