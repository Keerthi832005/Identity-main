using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Application.Messaging;

namespace Identity.Application.Tests;

public sealed class CatalogBulkTests
{
    private static BulkRow Row(int number, params (string Column, string? Value)[] cells) =>
        new(number, [.. cells.Select(cell => new BulkCell(cell.Column, cell.Value, cell.Value, false))]);

    private static BulkCommitCandidate Candidate(int number, string code, string? parent) =>
        new(number, new Dictionary<string, string?>
        {
            [CatalogBulkDescriptors.ModuleCode] = code,
            [CatalogBulkDescriptors.ParentModuleCode] = parent,
        });

    private static ModulesBulkCommitter Committer() =>
        new(new ThrowingDispatcher(), new StubCatalog());

    [Fact]
    public void ModuleParentIsCommittedBeforeItsChildEvenWhenItComesLaterInTheFile()
    {
        BulkCommitCandidate[] rows =
        [
            Candidate(3, "reports", "production"),
            Candidate(4, "production", null),
        ];

        var order = Committer().OrderForCommit(rows);

        Assert.Equal([4, 3], order);
    }

    [Fact]
    public void AWholeBranchIsOrderedRootFirst()
    {
        BulkCommitCandidate[] rows =
        [
            Candidate(3, "daily", "reports"),
            Candidate(4, "reports", "production"),
            Candidate(5, "production", null),
        ];

        var order = Committer().OrderForCommit(rows);

        Assert.Equal([5, 4, 3], order);
    }

    [Fact]
    public void FileOrderIsKeptWhenNothingReferencesAnythingElse()
    {
        BulkCommitCandidate[] rows =
        [
            Candidate(3, "production", null),
            Candidate(4, "quality", null),
        ];

        Assert.Equal([3, 4], Committer().OrderForCommit(rows));
    }

    [Fact]
    public void EveryRowIsStillCommittedWhenRowsReferenceEachOtherInALoop()
    {
        /* The validator rejects a cycle, so this is defence in depth: ordering must terminate and
           account for every row rather than recursing until the stack runs out. */
        BulkCommitCandidate[] rows =
        [
            Candidate(3, "a", "b"),
            Candidate(4, "b", "a"),
        ];

        var order = Committer().OrderForCommit(rows);

        Assert.Equal(2, order.Count);
        Assert.Contains(3, order);
        Assert.Contains(4, order);
    }

    [Fact]
    public async Task AModuleParentDefinedLaterInTheFileIsAccepted()
    {
        var validator = new ModulesBulkValidator(new StubCatalog("PTS"));
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "reports"),
                (CatalogBulkDescriptors.ParentModuleCode, "production")),
            Row(4, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "production")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Modules, rows, TestContext.Current.CancellationToken);

        Assert.All(verdicts, verdict => Assert.Empty(verdict.Errors));
    }

    [Fact]
    public async Task AnUnknownParentModuleSaysHowToFixIt()
    {
        var validator = new ModulesBulkValidator(new StubCatalog("PTS"));
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "reports"),
                (CatalogBulkDescriptors.ParentModuleCode, "missing")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Modules, rows, TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(CatalogBulkValidator.UnknownParentModule, error.Code);
        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModuleCannotBeItsOwnParent()
    {
        var validator = new ModulesBulkValidator(new StubCatalog("PTS"));
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "reports"),
                (CatalogBulkDescriptors.ParentModuleCode, "REPORTS")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Modules, rows, TestContext.Current.CancellationToken);

        Assert.Equal(CatalogBulkValidator.ParentIsSelf, Assert.Single(verdicts[0].Errors).Code);
    }

    [Fact]
    public async Task ALoopBetweenModulesIsRejected()
    {
        var validator = new ModulesBulkValidator(new StubCatalog("PTS"));
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "a"),
                (CatalogBulkDescriptors.ParentModuleCode, "b")),
            Row(4, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "b"),
                (CatalogBulkDescriptors.ParentModuleCode, "a")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Modules, rows, TestContext.Current.CancellationToken);

        Assert.Contains(
            verdicts.SelectMany(verdict => verdict.Errors),
            error => error.Code == CatalogBulkValidator.ParentCycle);
    }

    [Fact]
    public async Task AnUnknownApplicationIsReportedAgainstItsOwnCell()
    {
        var validator = new RolesBulkValidator(new StubCatalog());
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "NOPE"),
                (CatalogBulkDescriptors.RoleCode, "supervisor")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Roles, rows, TestContext.Current.CancellationToken);

        var error = Assert.Single(verdicts[0].Errors);
        Assert.Equal(CatalogBulkValidator.UnknownApplication, error.Code);
        Assert.Equal(CatalogBulkDescriptors.ApplicationCode, error.ColumnId);
    }

    [Fact]
    public async Task ACapabilityNeedsItsModuleToExistAlready()
    {
        var validator = new CapabilitiesBulkValidator(new StubCatalog("PTS"));
        BulkRow[] rows =
        [
            Row(3, (CatalogBulkDescriptors.ApplicationCode, "PTS"),
                (CatalogBulkDescriptors.ModuleCode, "not-yet"),
                (CatalogBulkDescriptors.CapabilityCode, "pts.reports.read")),
        ];

        var verdicts = await validator.Verify(
            CatalogBulkDescriptors.Capabilities, rows, TestContext.Current.CancellationToken);

        Assert.Equal(CatalogBulkValidator.UnknownModule, Assert.Single(verdicts[0].Errors).Code);
    }

    [Fact]
    public void NoCatalogTemplateCarriesAClientSecret()
    {
        var forbidden = new[] { "secret", "password", "token", "credential" };
        BulkEntityDescriptor[] descriptors =
        [
            CatalogBulkDescriptors.Modules,
            CatalogBulkDescriptors.Capabilities,
            CatalogBulkDescriptors.Roles,
        ];

        foreach (var column in descriptors.SelectMany(descriptor => descriptor.Columns))
        {
            Assert.DoesNotContain(
                forbidden,
                word => column.ColumnId.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || column.Header.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class StubCatalog(params string[] applicationCodes) : ICatalogCodeResolver
    {
        public Task<IReadOnlyDictionary<string, long>> FindApplicationIdsByCodes(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(
                codes
                    .Where(code => applicationCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(code => code, _ => 10L, StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, long>> FindModuleIdsByCodes(
            long applicationId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken) => Empty();

        public Task<IReadOnlyDictionary<string, long>> FindRoleIdsByCodes(
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

    /// <summary>Ordering never dispatches, so reaching the dispatcher here would be a defect.</summary>
    private sealed class ThrowingDispatcher : IRequestDispatcher
    {
        public ValueTask<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
