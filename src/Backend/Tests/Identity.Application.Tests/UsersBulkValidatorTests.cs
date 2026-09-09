using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Domain.Enums;

namespace Identity.Application.Tests;

public sealed class UsersBulkValidatorTests
{
    private static readonly BulkEntityDescriptor Descriptor = UsersBulkDescriptor.Descriptor;

    private static BulkRow Row(int number, params (string Column, string? Value)[] cells) =>
        new(number, [.. cells.Select(cell => new BulkCell(cell.Column, cell.Value, cell.Value, false))]);

    private static UsersBulkValidator Validator(params string[] existingCodes) =>
        new(new StubStore(existingCodes), new StubOrganizations());

    [Fact]
    public async Task ValidBranchCodePasses()
    {
        var rows = new[] { Row(3, ("employeeCode", "INDE05512"), ("displayName", "Harish"),
            ("branchCode", "BR-CHENNAI")) };

        var verdicts = await new UsersBulkValidator(new StubStore([]),
            new StubOrganizations(("BR-CHENNAI", OrganizationUnitType.Branch)))
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.Empty(verdicts[0].Errors);
    }

    [Fact]
    public async Task NonBranchCodeIsRejected()
    {
        var rows = new[] { Row(3, ("employeeCode", "INDE05512"), ("displayName", "Harish"),
            ("branchCode", "TN")) };

        var verdicts = await new UsersBulkValidator(new StubStore([]),
            new StubOrganizations(("TN", OrganizationUnitType.State)))
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.Equal(UsersBulkValidator.WrongBranchType, Assert.Single(verdicts[0].Errors).Code);
    }

    [Fact]
    public async Task ExistingEmployeeCodeIsClassifiedAsAnUpdate()
    {
        var rows = new[] { Row(3, ("employeeCode", "ADMIN-001"), ("displayName", "Admin")) };

        var verdicts = await Validator("ADMIN-001")
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.True(verdicts[0].MatchesExistingRecord);
        Assert.Empty(verdicts[0].Errors);
    }

    [Fact]
    public async Task UnknownEmployeeCodeIsClassifiedAsACreate()
    {
        var rows = new[] { Row(3, ("employeeCode", "INDE05512"), ("displayName", "Harish")) };

        var verdicts = await Validator()
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.False(verdicts[0].MatchesExistingRecord);
    }

    [Fact]
    public async Task ManagerCreatedEarlierInTheSameFileResolves()
    {
        var rows = new[]
        {
            Row(3, ("employeeCode", "INDE05512"), ("displayName", "Harish")),
            Row(4, ("employeeCode", "INDE05515"), ("displayName", "Deepa"),
                ("managerEmployeeCode", "INDE05512")),
        };

        var verdicts = await Validator()
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        /* The manager does not exist in the database yet; it is created by row 3 of this batch. */
        Assert.Empty(verdicts[1].Errors);
    }

    [Fact]
    public async Task UnknownManagerNamesTheCodeAndSaysHowToFixIt()
    {
        var rows = new[]
        {
            Row(3, ("employeeCode", "INDE05514"), ("displayName", "Arjun"),
                ("managerEmployeeCode", "INDE09999")),
        };

        var verdicts = await Validator()
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(UsersBulkValidator.UnknownManager, error.Code);
        Assert.Equal("managerEmployeeCode", error.ColumnId);
        Assert.Contains("INDE09999", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUserCannotBeTheirOwnManager()
    {
        var rows = new[]
        {
            Row(3, ("employeeCode", "INDE05512"), ("displayName", "Harish"),
                ("managerEmployeeCode", "inde05512")),
        };

        var verdicts = await Validator("INDE05512")
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.Equal(UsersBulkValidator.ManagerIsSelf, Assert.Single(verdicts[0].Errors).Code);
    }

    [Theory]
    [InlineData("r.menon@fujitec")]
    [InlineData("no-at-sign.example.com")]
    [InlineData("two@@at.com")]
    [InlineData("trailing@dot.")]
    [InlineData("has space@in.fujitec.com")]
    public async Task ImplausibleEmailIsReportedAgainstTheEmailCell(string email)
    {
        var rows = new[]
        {
            Row(3, ("employeeCode", "INDE05513"), ("displayName", "Meera"), ("email", email)),
        };

        var verdicts = await Validator()
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(UsersBulkValidator.EmailInvalid, error.Code);
        Assert.Equal("email", error.ColumnId);
    }

    [Fact]
    public async Task AValidEmailPasses()
    {
        var rows = new[]
        {
            Row(3, ("employeeCode", "INDE05513"), ("displayName", "Meera"),
                ("email", "meera.n@in.fujitec.com")),
        };

        var verdicts = await Validator()
            .Verify(Descriptor, rows, TestContext.Current.CancellationToken);

        Assert.Empty(verdicts[0].Errors);
    }

    [Fact]
    public void TheTemplateNeverExposesCredentialMaterial()
    {
        var forbidden = new[] { "password", "pin", "secret", "otp", "totp", "token" };

        foreach (var column in Descriptor.Columns)
        {
            Assert.DoesNotContain(
                forbidden,
                word => column.ColumnId.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || column.Header.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The validator depends only on code resolution, so the stub is one method.</summary>
    private sealed class StubStore(string[] existingCodes) : IUserCodeResolver
    {
        public Task<IReadOnlyDictionary<string, long>> FindUserIdsByEmployeeCodes(
            IReadOnlyCollection<string> employeeCodes,
            CancellationToken cancellationToken)
        {
            var matches = employeeCodes
                .Where(code => existingCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((code, index) => new { code, id = (long)(index + 1) })
                .ToDictionary(
                    entry => entry.code,
                    entry => entry.id,
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, long>>(matches);
        }
    }

    private sealed class StubOrganizations(params (string Code, OrganizationUnitType Type)[] units)
        : IOrganizationCodeResolver
    {
        public Task<IReadOnlyDictionary<string, OrganizationUnitRef>> FindUnitsByCodes(
            IReadOnlyCollection<string> unitCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, OrganizationUnitRef>>(
                units.Where(unit => unitCodes.Contains(unit.Code, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(unit => unit.Code,
                        unit => new OrganizationUnitRef(1, 1, unit.Type),
                        StringComparer.OrdinalIgnoreCase));
    }
}
