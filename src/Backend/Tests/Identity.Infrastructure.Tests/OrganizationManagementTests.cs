using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

[Collection("Organization SQL")]
public sealed class OrganizationManagementTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static AdministrationContext Actor() => new(null, Guid.NewGuid());

    [Fact]
    public async Task Search_PagesFiltersAndReadsEveryTypeWithoutLeakingOtherOrganizations()
    {
        var root = await Root();
        var country = await Child(root, OrganizationUnitType.Country, "COUNTRY");
        var region = await Child(country, OrganizationUnitType.Region, "REGION");
        var state = await Child(region, OrganizationUnitType.State, "STATE");
        var branch = await Child(state, OrganizationUnitType.Branch, "BRANCH");
        var location = await Child(branch, OrganizationUnitType.Location, "LOCATION");
        var department = await Child(root, OrganizationUnitType.Department, "DEPT");
        var team = await Child(department, OrganizationUnitType.Team, "TEAM");
        foreach (var node in new[] { root, country, region, state, branch, location, department, team })
        {
            var detail = await Read(node);
            Assert.Equal(node.UnitType, detail.UnitType);
            Assert.Equal(node.HierarchyPath, detail.HierarchyPath);
            Assert.Equal(8, detail.RowVersion.Length);
            var typed = await Send(new SearchOrganizationUnitsQuery(root.OrganizationId, node.UnitType, null, true, null, 0, 50, Actor()));
            Assert.Single(typed.Items);
            Assert.Equal(node.OrganizationUnitId, typed.Items[0].OrganizationUnitId);
        }
        var page = await Send(new SearchOrganizationUnitsQuery(root.OrganizationId, null, null, null, null, 2, 3, Actor()));
        Assert.Equal(8, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        var children = await Send(new SearchOrganizationUnitsQuery(root.OrganizationId, null, root.OrganizationUnitId, null, null, 0, 50, Actor()));
        Assert.Equal(2, children.TotalCount);
        var search = await Send(new SearchOrganizationUnitsQuery(root.OrganizationId, null, null, null, "  DEPT  ", 0, 1, Actor()));
        Assert.Single(search.Items);
        Assert.Equal(department.OrganizationUnitId, search.Items[0].OrganizationUnitId);
        var other = await Root();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Send(new GetOrganizationUnitQuery(other.OrganizationId, team.OrganizationUnitId, Actor())));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Send(new SearchOrganizationUnitsQuery(other.OrganizationId, null, root.OrganizationUnitId, null, null, 0, 50, Actor())));
    }

    [Fact]
    public async Task Updates_PersistSharedFieldsKeepIdentityAndAuditExactlyOnce()
    {
        var root = await Root();
        var unit = await Child(root, OrganizationUnitType.Department, "EDIT");
        var initial = await Read(unit);
        var actor = Actor();
        var address = new OrganizationUnitAddress(" Line one ", "Line two", "Line three", "City", "District", "Province", "600001", "IND", 13.123456m, 80.123456m);
        var command = new UpdateOrganizationUnitCommand(unit.OrganizationId, unit.OrganizationUnitId, " CHANGED ", " Changed name ", " Description ", address, initial.RowVersion, actor);
        var updated = await Send(command);
        Assert.Equal("CHANGED", updated.UnitCode);
        Assert.Equal("Changed name", updated.UnitName);
        Assert.Equal("Description", updated.Description);
        Assert.Equal("Line one", updated.Address.AddressLine1);
        Assert.Equal(address.Latitude, updated.Address.Latitude);
        Assert.Equal(initial.HierarchyPath, updated.HierarchyPath);
        Assert.Equal(initial.ParentOrganizationUnitId, updated.ParentOrganizationUnitId);
        Assert.Equal(initial.UnitType, updated.UnitType);
        Assert.NotEqual(initial.RowVersion, updated.RowVersion);
        var replay = await Send(command with { RowVersion = updated.RowVersion });
        Assert.Equal(updated.RowVersion, replay.RowVersion);
        Assert.Equal(updated.UpdatedAt, replay.UpdatedAt);
        await using var db = Context();
        var audit = await db.AuthenticationAudits.SingleAsync(x => x.CorrelationId == actor.CorrelationId, Token);
        Assert.Equal("OrganizationUnitUpdated", audit.EventType);
        Assert.DoesNotContain("Line one", audit.EventDataJson!);
        Assert.True(await db.Departments.AnyAsync(x => x.DepartmentId == unit.OrganizationUnitId, Token));
        var cleared = await Send(command with { Description = null, Address = new(), RowVersion = updated.RowVersion });
        Assert.Null(cleared.Description);
        Assert.Null(cleared.Address.City);
        Assert.Null(cleared.Address.Latitude);
    }

    [Fact]
    public async Task State_RejectsStaleWritesAndPreservesDescendants()
    {
        var root = await Root();
        var country = await Child(root, OrganizationUnitType.Country, "ACTIVE");
        var before = await Read(root);
        var actor = Actor();
        var inactive = await Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, false, before.RowVersion, actor));
        Assert.False(inactive.IsActive);
        Assert.True((await Read(country)).IsActive);
        Assert.Equal(inactive.RowVersion, (await Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, false, inactive.RowVersion, actor))).RowVersion);
        await Assert.ThrowsAsync<OrganizationConcurrencyException>(() => Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, true, before.RowVersion, actor)));
        await Assert.ThrowsAsync<OrganizationConcurrencyException>(() => Send(new UpdateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId, "STALE", "Stale", null, new(), before.RowVersion, actor)));
        await Assert.ThrowsAsync<ArgumentException>(() => Child(root, OrganizationUnitType.Department, "NOPE"));
        var active = await Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, true, inactive.RowVersion, actor));
        Assert.True(active.IsActive);
        await using var db = Context();
        Assert.Equal(2, await db.AuthenticationAudits.CountAsync(x => x.CorrelationId == actor.CorrelationId && x.EventType == "OrganizationUnitStateChanged", Token));
    }

    [Fact]
    public async Task Codes_AreUniqueAcrossTypesAndFailedUpdatesRollBackAudit()
    {
        var root = await Root();
        await Child(root, OrganizationUnitType.Country, "SAME");
        await Assert.ThrowsAsync<OrganizationCodeConflictException>(() => Child(root, OrganizationUnitType.Department, " same "));
        var department = await Child(root, OrganizationUnitType.Department, "OTHER");
        var before = await Read(department);
        var actor = Actor();
        await Assert.ThrowsAsync<OrganizationCodeConflictException>(() => Send(new UpdateOrganizationUnitCommand(root.OrganizationId, department.OrganizationUnitId, "same", "Conflict", null, new(), before.RowVersion, actor)));
        Assert.Equal(before.RowVersion, (await Read(department)).RowVersion);
        await using var db = Context();
        Assert.False(await db.AuthenticationAudits.AnyAsync(x => x.CorrelationId == actor.CorrelationId, Token));
        var other = await Root();
        await Child(other, OrganizationUnitType.Country, "SAME");
    }

    [Fact]
    public async Task Commands_RejectWrongParentOwnershipInvalidDetailsAndUnauthorizedAccess()
    {
        var root = await Root();
        var other = await Root();
        var before = await Read(root);
        await Assert.ThrowsAsync<ArgumentException>(() => Child(root, OrganizationUnitType.Team, "WRONG-TYPE"));
        await Assert.ThrowsAsync<ArgumentException>(() => Send(new CreateOrganizationUnitCommand(other.OrganizationId, root.OrganizationUnitId, OrganizationUnitType.Country, "X", "Wrong owner", Actor())));
        await Assert.ThrowsAsync<AdministrationException>(() => Send(new CreateOrganizationUnitCommand(root.OrganizationId, long.MaxValue, OrganizationUnitType.Country, "X", "Missing", Actor())));
        var update = new UpdateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId, "X", "Name", null, new(), before.RowVersion, Actor());
        await Assert.ThrowsAsync<ArgumentException>(() => Send(update with { Name = " " }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Send(update with { Address = new(Latitude: 91) }));
        await Assert.ThrowsAsync<ArgumentException>(() => Send(update with { RowVersion = [] }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Send(update with { OrganizationId = other.OrganizationId }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(update, true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(new SearchOrganizationUnitsQuery(null, null, null, null, null, 0, 20, Actor()), true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(new GetOrganizationUnitQuery(root.OrganizationId, root.OrganizationUnitId, Actor()), true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, false, before.RowVersion, Actor()), true));
        Assert.Equal(before.RowVersion, (await Read(root)).RowVersion);
    }

    [Fact]
    public async Task Search_RejectsUnboundedInvalidFiltersAndCancellation()
    {
        var query = new SearchOrganizationUnitsQuery(null, null, null, null, null, 0, 20, Actor());
        foreach (var invalid in new[] { query with { Skip = -1 }, query with { Take = 51 }, query with { Take = 0 }, query with { Search = new string('x', 101) }, query with { OrganizationId = 0 }, query with { UnitType = (OrganizationUnitType)999 } })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Send(invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => Send(query with { ParentOrganizationUnitId = 1 }));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Send(query, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ConcurrentEdits_AllowOnlyOneWriterAndRollbackFailedAudit()
    {
        var root = await Root();
        var before = await Read(root);
        var command = new UpdateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId, "WIN", "Writer", null, new(), before.RowVersion, Actor());
        async Task<bool> TryWrite(string name)
        {
            try { await Send(command with { Name = name }); return true; }
            catch (OrganizationConcurrencyException) { return false; }
        }
        var writes = await Task.WhenAll(TryWrite("First"), TryWrite("Second"));
        Assert.Single(writes, x => x);
        var current = await Read(root);
        await Assert.ThrowsAsync<DbUpdateException>(() => Send(command with { RowVersion = current.RowVersion, Name = "Invalid audit", Context = new(long.MaxValue, Guid.NewGuid()) }));
        Assert.Equal(current.RowVersion, (await Read(root)).RowVersion);
    }

    [Fact]
    public async Task ReusedDispatcherScope_RefreshesParentStateBeforeChildCreation()
    {
        await using var db = Context();
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(db.Database.GetConnectionString()!);
        services.AddSingleton<IAdministrationAuthorizer>(new TestAuthorizer(false));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var root = await dispatcher.Send(new CreateOrganizationCommand("ROOT", "Organization", Actor()), Token);
        await dispatcher.Send(new CreateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId,
            OrganizationUnitType.Country, "COUNTRY", "Country", Actor()), Token);
        var before = await Read(root);
        await Send(new SetOrganizationUnitActiveCommand(root.OrganizationId, root.OrganizationUnitId, false, before.RowVersion, Actor()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await dispatcher.Send(new CreateOrganizationUnitCommand(root.OrganizationId,
            root.OrganizationUnitId, OrganizationUnitType.Department, "DEPT", "Department", Actor()), Token));
    }

    private static Task<OrganizationHierarchyResult> Root() => Send(new CreateOrganizationCommand("ROOT", "Organization", Actor()));
    private static Task<OrganizationHierarchyResult> Child(OrganizationHierarchyResult parent, OrganizationUnitType type, string code) =>
     Send(new CreateOrganizationUnitCommand(parent.OrganizationId, parent.OrganizationUnitId, type, code, type.ToString(), Actor()));
    private static Task<OrganizationUnitDetails> Read(OrganizationHierarchyResult unit) => Send(new GetOrganizationUnitQuery(unit.OrganizationId, unit.OrganizationUnitId, Actor()));
    private static async Task<T> Send<T>(IRequest<T> command, bool deny = false, CancellationToken? cancellationToken = null)
    {
        await using var db = Context();
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(db.Database.GetConnectionString()!);
        services.AddSingleton<IAdministrationAuthorizer>(new TestAuthorizer(deny));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(command, cancellationToken ?? Token);
    }
    private static IdentityDbContext Context()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to an isolated migrated test database.");
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
        Assert.StartsWith("FIN_IAM_OrgMgmtTests_", target.InitialCatalog);
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).Options);
    }
    private sealed class TestAuthorizer(bool deny) : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) =>
         deny ? ValueTask.FromException(new UnauthorizedAccessException()) : ValueTask.CompletedTask;
    }
}
