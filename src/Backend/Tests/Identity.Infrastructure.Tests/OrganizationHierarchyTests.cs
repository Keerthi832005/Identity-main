using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

[Collection("Organization SQL")]
public sealed class OrganizationHierarchyTests
{
    [Fact]
    public async Task Hierarchy_PersistsBothBranchesAndCanonicalSharedDetails()
    {
        await using var db = CreateContext();
        var graph = await CreateHierarchy(db);
        Assert.Equal($"/{graph.Root.OrganizationUnitId}/", graph.Root.HierarchyPath);
        Assert.Equal(graph.Branch.HierarchyPath + graph.Location.OrganizationUnitId + "/", graph.Location.HierarchyPath);
        Assert.Equal(graph.Root.HierarchyPath + graph.Department.OrganizationUnitId + "/", graph.Department.HierarchyPath);
        Assert.Equal(graph.Department.HierarchyPath + graph.Team.OrganizationUnitId + "/", graph.Team.HierarchyPath);
        Assert.All(new[] { graph.Root, graph.Country, graph.Region, graph.State, graph.Branch, graph.Location, graph.Department, graph.Team },
            unit => Assert.Equal(8, unit.RowVersion.Length));
        graph.Branch.SetAddress(new OrganizationUnitAddress(AddressLine1: "Test street", City: "Test city", CountryCode: "IN",
            Latitude: 13.0827m, Longitude: 80.2707m), DateTime.UtcNow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var city = await db.Database.SqlQuery<string>(
            $"SELECT [City] AS [Value] FROM [Identity].[BranchDetails] WHERE [BranchId] = {graph.Branch.OrganizationUnitId}")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Test city", city);
        var loaded = await db.Branches.AsNoTracking().Include(value => value.Unit)
            .SingleAsync(value => value.BranchId == graph.Branch.OrganizationUnitId, TestContext.Current.CancellationToken);
        Assert.Equal(graph.Branch.OrganizationUnitId, loaded.OrganizationUnitId);
        Assert.Equal(OrganizationUnitType.Branch, loaded.UnitType);
        Assert.Equal("Test city", loaded.Unit.City);
    }

    [Fact]
    public async Task Database_RejectsCrossOrganizationAndInvalidParentTypes()
    {
        await using var db = CreateContext();
        var graph = await CreateHierarchy(db);
        var other = await CreateRoot(db);
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, other.OrganizationId, graph.Country.OrganizationUnitId, "Region"));
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, graph.Root.OrganizationId, graph.Branch.OrganizationUnitId, "Department"));
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, graph.Root.OrganizationId, graph.Root.OrganizationUnitId, "Team"));
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, graph.Root.OrganizationId, null, "Country"));
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, graph.Root.OrganizationId, graph.Root.OrganizationUnitId, "Unknown"));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[OrganizationUnit] SET [ParentOrganizationUnitId] = [OrganizationUnitId] WHERE [OrganizationUnitId] = {graph.Country.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_EnforcesOneRootAndNormalizedCodesWithinEachOrganization()
    {
        await using var db = CreateContext();
        var first = await CreateRoot(db);
        var second = await CreateRoot(db);
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, first.OrganizationId, null, "Organization"));
        const string code = "ORG-TEST-SAME-CODE";
        await InsertUnit(db, first.OrganizationId, first.OrganizationUnitId, "Country", code);
        await InsertUnit(db, second.OrganizationId, second.OrganizationUnitId, "Country", code);
        await Assert.ThrowsAsync<SqlException>(() => InsertUnit(db, first.OrganizationId, first.OrganizationUnitId,
            "Department", " org-test-same-code "));
    }

    [Fact]
    public async Task TypedLinks_RejectWrongTypeWrongOrganizationAndParentMismatch()
    {
        await using var db = CreateContext();
        var graph = await CreateHierarchy(db);
        var anotherRoot = await CreateRoot(db);
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT [Identity].[Country] ([CountryId], [OrganizationId]) VALUES ({graph.Department.OrganizationUnitId}, {graph.Root.OrganizationId})",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[Region] SET [OrganizationId] = {anotherRoot.OrganizationId} WHERE [RegionId] = {graph.Region.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
        var otherCountry = await AddChild(db, graph.Root, OrganizationUnitType.Country);
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[Region] SET [CountryId] = {otherCountry.OrganizationUnitId} WHERE [RegionId] = {graph.Region.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[Country] SET [UnitType] = N'Region' WHERE [CountryId] = {graph.Country.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_RejectsInvalidPathsParentMismatchAndCascadingDelete()
    {
        await using var db = CreateContext();
        var graph = await CreateHierarchy(db);
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[OrganizationUnit] SET [HierarchyPath] = N'' WHERE [OrganizationUnitId] = {graph.Location.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
        Assert.Equal(graph.Branch.HierarchyPath + graph.Location.OrganizationUnitId + "/", graph.Location.HierarchyPath);
        var newCountry = await AddChild(db, graph.Root, OrganizationUnitType.Country);
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[OrganizationUnit] SET [ParentOrganizationUnitId] = {newCountry.OrganizationUnitId} WHERE [OrganizationUnitId] = {graph.Region.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [Identity].[OrganizationUnit] WHERE [OrganizationUnitId] = {graph.Root.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [Identity].[Organization] WHERE [OrganizationId] = {graph.Root.OrganizationId}",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[OrganizationUnit] SET [Latitude] = 91 WHERE [OrganizationUnitId] = {graph.Branch.OrganizationUnitId}",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RowVersion_RejectsStaleUpdatesAndActiveStateDoesNotDeleteDescendants()
    {
        await using var first = CreateContext();
        var root = await CreateRoot(first);
        var country = await AddChild(first, root, OrganizationUnitType.Country);
        await using var second = CreateContext();
        var stale = await second.OrganizationUnits.SingleAsync(x => x.OrganizationUnitId == root.OrganizationUnitId,
            TestContext.Current.CancellationToken);
        root.SetActive(false, DateTime.UtcNow);
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        stale.UpdateDetails(stale.UnitCode, "Stale update", null, DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.True(await first.Countries.AnyAsync(x => x.CountryId == country.OrganizationUnitId, TestContext.Current.CancellationToken));
    }

    private static async Task<Hierarchy> CreateHierarchy(IdentityDbContext db)
    {
        var root = await CreateRoot(db);
        var country = await AddChild(db, root, OrganizationUnitType.Country);
        var region = await AddChild(db, country, OrganizationUnitType.Region);
        var state = await AddChild(db, region, OrganizationUnitType.State);
        var branch = await AddChild(db, state, OrganizationUnitType.Branch);
        var location = await AddChild(db, branch, OrganizationUnitType.Location);
        var department = await AddChild(db, root, OrganizationUnitType.Department);
        var team = await AddChild(db, department, OrganizationUnitType.Team);
        return new Hierarchy(root, country, region, state, branch, location, department, team);
    }

    [Fact]
    public async Task CreationWorkflow_RollsBackAnchorWhenRootOrAuditFails()
    {
        await using var db = CreateContext();
        var anchors = await db.Organizations.CountAsync(TestContext.Current.CancellationToken);
        var units = await db.OrganizationUnits.CountAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(() => Send(db, new CreateOrganizationCommand("", "Invalid root",
            new AdministrationContext(null, Guid.NewGuid()))));
        await Assert.ThrowsAsync<DbUpdateException>(() => Send(db, new CreateOrganizationCommand("AUDIT-ROLLBACK", "Audit rollback",
            new AdministrationContext(long.MaxValue, Guid.NewGuid()))));
        Assert.Equal(anchors, await db.Organizations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(units, await db.OrganizationUnits.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreationWorkflow_AuthorizesAuditsAndNeverCommitsNullPaths()
    {
        await using var db = CreateContext();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var command = new CreateOrganizationCommand("CONTROLLED-ROOT", "Organization", context);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(db, command, deny: true));
        var root = await Send(db, command);
        var child = await Send(db, new CreateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId,
            OrganizationUnitType.Country, "IN", "India", context));
        Assert.Equal(root.HierarchyPath + child.OrganizationUnitId + "/", child.HierarchyPath);
        Assert.Equal(1, await db.OrganizationUnits.CountAsync(x => x.OrganizationId == root.OrganizationId
            && x.UnitType == OrganizationUnitType.Organization, TestContext.Current.CancellationToken));
        Assert.False(await db.OrganizationUnits.AnyAsync(x => x.OrganizationId == root.OrganizationId && x.HierarchyPath == null,
            TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.AuthenticationAudits.CountAsync(x => x.CorrelationId == context.CorrelationId,
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AdministrationException>(() => Send(db, new CreateOrganizationUnitCommand(root.OrganizationId,
            root.OrganizationUnitId, OrganizationUnitType.Organization, "SECOND-ROOT", "Wrong root", context)));
    }

    private static async Task<OrganizationUnit> CreateRoot(IdentityDbContext db)
    {
        var result = await Send(db, new CreateOrganizationCommand($"ROOT-{Guid.NewGuid():N}", "Organization",
            new AdministrationContext(null, Guid.NewGuid())));
        return await db.OrganizationUnits.SingleAsync(x => x.OrganizationUnitId == result.OrganizationUnitId,
            TestContext.Current.CancellationToken);
    }

    private static async Task<OrganizationUnit> AddChild(IdentityDbContext db, OrganizationUnit parent, OrganizationUnitType type)
    {
        var result = await Send(db, new CreateOrganizationUnitCommand(parent.OrganizationId, parent.OrganizationUnitId,
            type, $"NODE-{Guid.NewGuid():N}", type.ToString(), new AdministrationContext(null, Guid.NewGuid())));
        return await db.OrganizationUnits.SingleAsync(x => x.OrganizationUnitId == result.OrganizationUnitId,
            TestContext.Current.CancellationToken);
    }

    private static async Task<OrganizationHierarchyResult> Send(IdentityDbContext db,
        IRequest<OrganizationHierarchyResult> command, bool deny = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer>(new TestAuthorizer(deny));
        services.AddIdentityApplication();
        services.AddIdentityPersistence(db.Database.GetConnectionString()!);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(command, TestContext.Current.CancellationToken);
    }

    private sealed class TestAuthorizer(bool deny) : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) =>
            deny ? ValueTask.FromException(new UnauthorizedAccessException("Denied")) : ValueTask.CompletedTask;
    }

    private static Task<int> InsertUnit(IdentityDbContext db, long organizationId, long? parentId, string type, string? code = null) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT [Identity].[OrganizationUnit] ([OrganizationId], [ParentOrganizationUnitId], [UnitType], [UnitCode], [UnitName]) VALUES ({organizationId}, {parentId}, {type}, {code ?? Guid.NewGuid().ToString("N")}, N'Test unit')",
            TestContext.Current.CancellationToken);

    private static IdentityDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to an isolated migrated test database.");
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).Options);
    }

    private sealed record Hierarchy(OrganizationUnit Root, OrganizationUnit Country, OrganizationUnit Region,
        OrganizationUnit State, OrganizationUnit Branch, OrganizationUnit Location, OrganizationUnit Department, OrganizationUnit Team);
}
