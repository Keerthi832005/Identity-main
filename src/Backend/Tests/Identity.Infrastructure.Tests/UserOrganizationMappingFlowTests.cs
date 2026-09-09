using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class UserOrganizationMappingFlowTests
{
    [Fact]
    public async Task Mapping_IsValidatedPersistedAuditedAndBackwardCompatibleWithoutGrantingAccess()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Requires an isolated migrated SQL test database.");
        Assert.StartsWith("FIN_IAM_OrgMgmtTests_", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var authorization = new TestAuthorizer();
        var services = new ServiceCollection().AddSingleton<IAdministrationAuthorizer>(authorization);
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        await using var provider = services.BuildServiceProvider();
        var token = TestContext.Current.CancellationToken;
        var context = new AdministrationContext(null, Guid.NewGuid());
        async Task<T> Send<T>(IRequest<T> command)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(command, token);
        }
        var root = await Send(new CreateOrganizationCommand($"ORG-{Guid.NewGuid():N}", "Mapping organization", context));
        Task<OrganizationHierarchyResult> Child(long parent, OrganizationUnitType type, string name) => Send(
            new CreateOrganizationUnitCommand(root.OrganizationId, parent, type, $"M-{Guid.NewGuid():N}", name, context));
        var department = await Child(root.OrganizationUnitId, OrganizationUnitType.Department, "Assembly");
        var team = await Child(department.OrganizationUnitId, OrganizationUnitType.Team, "Day shift");
        var otherDepartment = await Child(root.OrganizationUnitId, OrganizationUnitType.Department, "Maintenance");
        var otherTeam = await Child(otherDepartment.OrganizationUnitId, OrganizationUnitType.Team, "Repairs");
        var mapping = new UserOrganizationMapping(department.OrganizationUnitId, team.OrganizationUnitId);
        var code = $"MAP-{Guid.NewGuid():N}-X";
        var created = await Send(new CreateUserCommand(code, "Mapped employee", context, OrganizationMapping: mapping));
        var actor = context with { ActorUserId = created.ResourceId };
        UpdateUserProfileCommand Update(UserOrganizationMapping? value, string name = "Mapped employee") =>
            new(created.ResourceId, name, null, null, actor, value);
        await using var inspect = provider.CreateAsyncScope();
        var db = inspect.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var discovery = new AdministrationDiscoveryStore(db);
        var search = await Send(new SearchAdministrationUsersQuery(code, 0, 50));
        Assert.Equal("Assembly", Assert.Single(search.Items).DepartmentName);
        Assert.Equal("Day shift", search.Items[0].TeamName);
        var access = await discovery.GetUserAccessCatalog(created.ResourceId, DateTime.UtcNow, token);
        var security = await discovery.GetUserSecurityCatalog(created.ResourceId, DateTime.UtcNow, token);
        Assert.Equal("Assembly", access!.User.DepartmentName);
        Assert.Equal("Day shift", security!.User.TeamName);
        await Send(Update(mapping)); // No-op must not create a change audit.
        Assert.Equal(0, await db.AuthenticationAudits.CountAsync(x => x.UserId == created.ResourceId && x.EventType == "UserProfileUpdated", token));
        await Send(Update(null, "Renamed employee")); // Old clients omit the object.
        var persisted = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.UserId == created.ResourceId, token);
        Assert.Equal(mapping.DepartmentId, persisted.DepartmentId);
        Assert.Equal(mapping.TeamId, persisted.TeamId);
        Assert.Equal(1, persisted.SecurityVersion);
        Assert.False(await db.UserApplications.AnyAsync(x => x.UserId == created.ResourceId, token));
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == created.ResourceId, token));
        Assert.False(await db.UserCredentials.AnyAsync(x => x.UserId == created.ResourceId, token));

        foreach (var invalid in new[] {
            new UserOrganizationMapping(null, team.OrganizationUnitId),
            new UserOrganizationMapping(-1, null),
            new UserOrganizationMapping(long.MaxValue, null),
            new UserOrganizationMapping(root.OrganizationUnitId, null),
            new UserOrganizationMapping(department.OrganizationUnitId, otherTeam.OrganizationUnitId),
            new UserOrganizationMapping(otherDepartment.OrganizationUnitId, otherDepartment.OrganizationUnitId),
            new UserOrganizationMapping(department.OrganizationUnitId, long.MaxValue),
        })
            await Assert.ThrowsAsync<AdministrationException>(async () => await Send(Update(invalid)));
        Assert.Equal(1, await db.AuthenticationAudits.CountAsync(x => x.UserId == created.ResourceId && x.EventType == "UserProfileUpdated", token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[UserAccount] SET [TeamId]={otherTeam.OrganizationUnitId} WHERE [UserId]={created.ResourceId}", token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[UserAccount] SET [DepartmentId]=NULL WHERE [UserId]={created.ResourceId}", token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[UserAccount] SET [DepartmentId]={long.MaxValue}, [TeamId]=NULL WHERE [UserId]={created.ResourceId}", token));

        // Retain existing inactive mappings during unrelated edits; permit removal, but not new assignments.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[OrganizationUnit] SET [IsActive]=0 WHERE [OrganizationUnitId]={team.OrganizationUnitId}", token);
        await Send(Update(mapping, "Inactive team retained"));
        await Send(Update(new(mapping.DepartmentId, null), "Team cleared"));
        await Assert.ThrowsAsync<AdministrationException>(async () => await Send(Update(mapping)));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[OrganizationUnit] SET [IsActive]=1 WHERE [OrganizationUnitId]={team.OrganizationUnitId}", token);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[OrganizationUnit] SET [IsActive]=0 WHERE [OrganizationUnitId]={root.OrganizationUnitId}", token);
        await Assert.ThrowsAsync<AdministrationException>(async () => await Send(Update(mapping)));
        await Send(Update(new(null, null), "Unassigned"));
        await Assert.ThrowsAsync<AdministrationException>(async () => await Send(Update(new(mapping.DepartmentId, null))));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[OrganizationUnit] SET [IsActive]=1 WHERE [OrganizationUnitId]={root.OrganizationUnitId}", token);
        authorization.Deny = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Send(Update(mapping)));
        authorization.Deny = false;
        persisted = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.UserId == created.ResourceId, token);
        Assert.Null(persisted.DepartmentId);
        Assert.Null(persisted.TeamId);
        Assert.Equal(1, persisted.SecurityVersion);
        Assert.Equal(4, await db.AuthenticationAudits.CountAsync(x => x.UserId == created.ResourceId && x.EventType == "UserProfileUpdated", token));
    }

    private sealed class TestAuthorizer : IAdministrationAuthorizer
    {
        public bool Deny { get; set; }
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken token)
        {
            if (Deny) throw new UnauthorizedAccessException();
            return ValueTask.CompletedTask;
        }
    }
}
