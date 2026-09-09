using Identity.Application.Administration;
using Identity.Application.BulkData;

namespace Identity.Application.Tests;

public sealed class AccessGrantBulkTests
{
    private static BulkRow Row(int number, params (string Column, string? Value)[] cells) =>
        new(number, [.. cells.Select(cell => new BulkCell(cell.Column, cell.Value, cell.Value, false))]);

    private static BulkRow Grant(int number, string employee, string application) =>
        Row(number,
            (AccessGrantBulkDescriptors.EmployeeCode, employee),
            (AccessGrantBulkDescriptors.ApplicationCode, application));

    private static BulkRow Assignment(int number, string employee, string application, string role) =>
        Row(number,
            (AccessGrantBulkDescriptors.EmployeeCode, employee),
            (AccessGrantBulkDescriptors.ApplicationCode, application),
            (AccessGrantBulkDescriptors.RoleCode, role));

    [Fact]
    public async Task AnUnknownUserIsReportedAgainstTheEmployeeCodeCell()
    {
        var validator = new UserApplicationsBulkValidator(
            new StubUsers(), new StubCatalog("PTS"), new StubStore());

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserApplications,
            [Grant(3, "NOBODY", "PTS")],
            TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(AccessGrantBulkValidator.UnknownUser, error.Code);
        Assert.Equal(AccessGrantBulkDescriptors.EmployeeCode, error.ColumnId);
        Assert.Contains("Import the users file first", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownApplicationIsReportedAgainstItsOwnCell()
    {
        var validator = new UserApplicationsBulkValidator(
            new StubUsers("INDE03275"), new StubCatalog(), new StubStore());

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserApplications,
            [Grant(3, "INDE03275", "NOPE")],
            TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(AccessGrantBulkValidator.UnknownApplication, error.Code);
        Assert.Equal(AccessGrantBulkDescriptors.ApplicationCode, error.ColumnId);
    }

    [Fact]
    public async Task BothUnknownReferencesAreReportedTogether()
    {
        var validator = new UserApplicationsBulkValidator(
            new StubUsers(), new StubCatalog(), new StubStore());

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserApplications,
            [Grant(3, "NOBODY", "NOPE")],
            TestContext.Current.CancellationToken);

        /* One round trip should tell the administrator everything wrong with the row. */
        Assert.Equal(2, verdicts[0].Errors.Count);
    }

    [Fact]
    public async Task AGrantThatAlreadyExistsIsAnUpdateRatherThanACreate()
    {
        var validator = new UserApplicationsBulkValidator(
            new StubUsers("INDE03275"), new StubCatalog("PTS"), new StubStore(grantExists: true));

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserApplications,
            [Grant(3, "INDE03275", "PTS")],
            TestContext.Current.CancellationToken);

        Assert.True(verdicts[0].MatchesExistingRecord);
        Assert.Empty(verdicts[0].Errors);
    }

    [Fact]
    public async Task ARoleFromAnotherApplicationSaysToCheckTheApplicationCode()
    {
        var validator = new UserRolesBulkValidator(
            new StubUsers("INDE03275"), new StubCatalog("PTS"), new StubStore());

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserRoles,
            [Assignment(3, "INDE03275", "PTS", "elsewhere")],
            TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(AccessGrantBulkValidator.UnknownRole, error.Code);
        Assert.Equal(AccessGrantBulkDescriptors.RoleCode, error.ColumnId);
        Assert.Contains("check the application code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKnownRoleAssignmentValidates()
    {
        var validator = new UserRolesBulkValidator(
            new StubUsers("INDE03275"),
            new StubCatalog("PTS") { Roles = ["supervisor"] },
            new StubStore());

        var verdicts = await validator.Verify(
            AccessGrantBulkDescriptors.UserRoles,
            [Assignment(3, "INDE03275", "PTS", "supervisor")],
            TestContext.Current.CancellationToken);

        Assert.Empty(verdicts[0].Errors);
    }

    [Fact]
    public void NeitherAccessTemplateCarriesASurrogateIdOrASecret()
    {
        var forbidden = new[] { "id", "secret", "password", "token" };
        BulkEntityDescriptor[] descriptors =
        [
            AccessGrantBulkDescriptors.UserApplications,
            AccessGrantBulkDescriptors.UserRoles,
        ];

        foreach (var column in descriptors.SelectMany(descriptor => descriptor.Columns))
        {
            /* "Employee code" contains no forbidden word; a column named userId would. */
            Assert.DoesNotContain(
                forbidden,
                word => string.Equals(column.ColumnId, word, StringComparison.OrdinalIgnoreCase)
                    || column.ColumnId.EndsWith(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void BothAccessTemplatesAreKeyedByNaturalCode()
    {
        Assert.Equal(
            [AccessGrantBulkDescriptors.EmployeeCode, AccessGrantBulkDescriptors.ApplicationCode],
            AccessGrantBulkDescriptors.UserApplications.KeyColumnIds);
        Assert.Equal(
            [
                AccessGrantBulkDescriptors.EmployeeCode,
                AccessGrantBulkDescriptors.ApplicationCode,
                AccessGrantBulkDescriptors.RoleCode,
            ],
            AccessGrantBulkDescriptors.UserRoles.KeyColumnIds);
    }

    private sealed class StubUsers(params string[] codes) : IUserCodeResolver
    {
        public Task<IReadOnlyDictionary<string, long>> FindUserIdsByEmployeeCodes(
            IReadOnlyCollection<string> employeeCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(
                employeeCodes
                    .Where(code => codes.Contains(code, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(code => code, _ => 42L, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class StubCatalog(params string[] applicationCodes) : ICatalogCodeResolver
    {
        public string[] Roles { get; init; } = [];

        public Task<IReadOnlyDictionary<string, long>> FindApplicationIdsByCodes(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(
                codes
                    .Where(code => applicationCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(code => code, _ => 10L, StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, long>> FindRoleIdsByCodes(
            long applicationId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(
                codes
                    .Where(code => Roles.Contains(code, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(code => code, _ => 30L, StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, long>> FindModuleIdsByCodes(
            long applicationId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) => Empty();

        public Task<IReadOnlyDictionary<string, long>> FindCapabilityIdsByCodes(
            long applicationId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) => Empty();

        private static Task<IReadOnlyDictionary<string, long>> Empty() =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Two methods, because that is all access-grant validation needs.</summary>
    private sealed class StubStore(bool grantExists = false, bool roleAssigned = false)
        : IAccessGrantLookup
    {
        public ValueTask<bool> HasApplicationGrant(
            long userId,
            long applicationId,
            CancellationToken cancellationToken) => ValueTask.FromResult(grantExists);

        public ValueTask<bool> HasRoleAssignment(
            long userId,
            long applicationId,
            long roleId,
            CancellationToken cancellationToken) => ValueTask.FromResult(roleAssigned);
    }
}
