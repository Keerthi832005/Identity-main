using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Domain.Enums;

namespace Identity.Application.Tests;

public sealed class OrganizationBulkTests
{
    private static BulkRow Row(int number, string type, string code, string? parent = null) =>
        new(number, [
            Cell(OrganizationBulkDescriptor.UnitType, type),
            Cell(OrganizationBulkDescriptor.UnitCode, code),
            Cell(OrganizationBulkDescriptor.UnitName, code + " name"),
            Cell(OrganizationBulkDescriptor.ParentUnitCode, parent),
        ]);

    private static BulkCell Cell(string columnId, string? value) =>
        new(columnId, value, value, false);

    private static async Task<IReadOnlyList<BulkEntityRowVerdict>> Verify(
        StubOrganizations existing,
        params BulkRow[] rows) =>
        await new OrganizationBulkValidator(existing).Verify(
            OrganizationBulkDescriptor.Descriptor, rows, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("Country", "Organization")]
    [InlineData("Region", "Country")]
    [InlineData("State", "Region")]
    [InlineData("Branch", "State")]
    [InlineData("Location", "Branch")]
    [InlineData("Department", "Organization")]
    [InlineData("Team", "Department")]
    public async Task EveryApprovedParentPairingIsAccepted(string childType, string parentType)
    {
        var existing = new StubOrganizations(("PARENT", Enum.Parse<OrganizationUnitType>(parentType)));

        var verdicts = await Verify(existing, Row(3, childType, "CHILD", "PARENT"));

        Assert.Empty(verdicts[0].Errors);
    }

    [Theory]
    [InlineData("Team", "Country")]
    [InlineData("Branch", "Organization")]
    [InlineData("Region", "State")]
    [InlineData("Location", "Region")]
    public async Task AnUnapprovedParentPairingIsRejectedAndNamesBothTypes(
        string childType,
        string parentType)
    {
        var existing = new StubOrganizations(("PARENT", Enum.Parse<OrganizationUnitType>(parentType)));

        var verdicts = await Verify(existing, Row(3, childType, "CHILD", "PARENT"));

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(OrganizationBulkValidator.WrongParentType, error.Code);
        Assert.Contains(childType, error.Message, StringComparison.Ordinal);
        Assert.Contains(parentType, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOrganizationRowMustNotNameAParent()
    {
        var verdicts = await Verify(
            new StubOrganizations(("PARENT", OrganizationUnitType.Organization)),
            Row(3, "Organization", "ROOT", "PARENT"));

        Assert.Equal(
            OrganizationBulkValidator.RootHasParent,
            Assert.Single(verdicts[0].Errors).Code);
    }

    [Fact]
    public async Task ANonRootRowMustNameAParentAndSaysWhichType()
    {
        var verdicts = await Verify(new StubOrganizations(), Row(3, "Branch", "CHENNAI"));

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(OrganizationBulkValidator.ParentRequired, error.Code);
        Assert.Contains("State", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AParentDefinedLaterInTheSameFileIsAccepted()
    {
        var verdicts = await Verify(
            new StubOrganizations(),
            Row(3, "Country", "IN", "FUJITEC"),
            Row(4, "Organization", "FUJITEC"));

        Assert.All(verdicts, verdict => Assert.Empty(verdict.Errors));
    }

    [Fact]
    public async Task AWholeBranchValidatesFromOneFileInAnyOrder()
    {
        var verdicts = await Verify(
            new StubOrganizations(),
            Row(3, "Branch", "CHENNAI", "TN"),
            Row(4, "State", "TN", "SOUTH"),
            Row(5, "Region", "SOUTH", "IN"),
            Row(6, "Country", "IN", "FUJITEC"),
            Row(7, "Organization", "FUJITEC"));

        Assert.All(verdicts, verdict => Assert.Empty(verdict.Errors));
    }

    [Fact]
    public async Task AnUnknownUnitTypeListsTheEightAllowedValues()
    {
        var verdicts = await Verify(new StubOrganizations(), Row(3, "Division", "X", "Y"));

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(OrganizationBulkValidator.UnknownType, error.Code);
        Assert.Contains("Organization", error.Message, StringComparison.Ordinal);
        Assert.Contains("Team", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUnitCannotBeItsOwnParent()
    {
        var verdicts = await Verify(new StubOrganizations(), Row(3, "Country", "IN", "in"));

        Assert.Equal(
            OrganizationBulkValidator.ParentIsSelf,
            Assert.Single(verdicts[0].Errors).Code);
    }

    [Fact]
    public async Task AnUnknownParentSaysHowToFixIt()
    {
        var verdicts = await Verify(new StubOrganizations(), Row(3, "Country", "IN", "MISSING"));

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(OrganizationBulkValidator.UnknownParent, error.Code);
        Assert.Contains("MISSING", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExistingCodeIsClassifiedAsAnUpdate()
    {
        var existing = new StubOrganizations(
            ("FUJITEC", OrganizationUnitType.Organization),
            ("IN", OrganizationUnitType.Country));

        var verdicts = await Verify(existing, Row(3, "Country", "IN", "FUJITEC"));

        Assert.True(verdicts[0].MatchesExistingRecord);
    }

    [Fact]
    public void ParentsAreCommittedBeforeChildrenWhateverTheFileOrder()
    {
        var committer = new OrganizationBulkCommitter(null!, new StubOrganizations());
        BulkCommitCandidate[] rows =
        [
            Candidate(3, "CHENNAI", "TN"),
            Candidate(4, "TN", "SOUTH"),
            Candidate(5, "SOUTH", null),
        ];

        Assert.Equal([5, 4, 3], committer.OrderForCommit(rows));
    }

    [Fact]
    public void OrderingTerminatesAndKeepsEveryRowWhenUnitsReferenceEachOtherInALoop()
    {
        var committer = new OrganizationBulkCommitter(null!, new StubOrganizations());
        BulkCommitCandidate[] rows = [Candidate(3, "A", "B"), Candidate(4, "B", "A")];

        var order = committer.OrderForCommit(rows);

        Assert.Equal(2, order.Count);
    }

    [Fact]
    public void TheTemplateOffersExactlyTheEightApprovedTypes()
    {
        var column = OrganizationBulkDescriptor.Descriptor.Columns
            .Single(candidate => candidate.ColumnId == OrganizationBulkDescriptor.UnitType);

        Assert.Equal(8, column.AllowedValues?.Count);
        Assert.Equal(
            Enum.GetNames<OrganizationUnitType>().Order(StringComparer.Ordinal),
            column.AllowedValues!.Order(StringComparer.Ordinal));
    }

    private static BulkCommitCandidate Candidate(int number, string code, string? parent) =>
        new(number, new Dictionary<string, string?>
        {
            [OrganizationBulkDescriptor.UnitCode] = code,
            [OrganizationBulkDescriptor.ParentUnitCode] = parent,
        });

    private sealed class StubOrganizations(params (string Code, OrganizationUnitType Type)[] units)
        : IOrganizationCodeResolver
    {
        public Task<IReadOnlyDictionary<string, OrganizationUnitRef>> FindUnitsByCodes(
            IReadOnlyCollection<string> unitCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, OrganizationUnitRef>>(
                units
                    .Where(unit => unitCodes.Contains(unit.Code, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(
                        unit => unit.Code,
                        unit => new OrganizationUnitRef(1, 1, unit.Type),
                        StringComparer.OrdinalIgnoreCase));
    }
}
