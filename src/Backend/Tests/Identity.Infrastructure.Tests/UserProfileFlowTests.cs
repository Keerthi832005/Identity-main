using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class UserProfileFlowTests
{
    [Fact]
    public async Task ProfileFlow_PersistsSearchesClearsAndAuditsWithoutChangingPermissions()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var manager = await CreateUser(dispatcher, context, "Manager");
        var email = $"employee-{Guid.NewGuid():N}@example.com";
        var employee = await dispatcher.Send(new CreateUserCommand($"EMP-{Guid.NewGuid():N}-X", "Employee",
            context, email, manager.ResourceId), TestContext.Current.CancellationToken);
        var actor = context with { ActorUserId = employee.ResourceId };
        var search = await dispatcher.Send(new SearchAdministrationUsersQuery(email, 0, 50), TestContext.Current.CancellationToken);
        var user = Assert.Single(search.Items);
        Assert.Equal(email, user.Email);
        Assert.Equal(manager.ResourceId, user.ManagerUserId);
        Assert.Equal("Manager", user.ManagerDisplayName);
        var update = new UpdateUserProfileCommand(employee.ResourceId, "Updated employee", null, null, actor);
        await dispatcher.Send(update, TestContext.Current.CancellationToken);
        await dispatcher.Send(update, TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var persisted = await db.UserAccounts.SingleAsync(x => x.UserId == employee.ResourceId, TestContext.Current.CancellationToken);
        Assert.Null(persisted.Email);
        Assert.Null(persisted.ManagerUserId);
        Assert.Equal(1, persisted.SecurityVersion);
        Assert.Equal(1, await db.AuthenticationAudits.CountAsync(x => x.UserId == employee.ResourceId
            && x.EventType == "UserProfileUpdated", TestContext.Current.CancellationToken));
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == employee.ResourceId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProfileFlow_RejectsMissingInactiveSelfAndDescendantManagers()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var root = await CreateUser(dispatcher, context, "Root");
        var child = await CreateUser(dispatcher, context, "Child", root.ResourceId);
        var grandchild = await CreateUser(dispatcher, context, "Grandchild", child.ResourceId);
        foreach (var invalidManager in new[] { root.ResourceId, grandchild.ResourceId, long.MaxValue })
        {
            await Assert.ThrowsAsync<AdministrationException>(async () => await dispatcher.Send(
                new UpdateUserProfileCommand(root.ResourceId, "Root", null, invalidManager, context),
                TestContext.Current.CancellationToken));
        }

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var inactive = await db.UserAccounts.SingleAsync(x => x.UserId == grandchild.ResourceId, TestContext.Current.CancellationToken);
        inactive.SetActive(false, DateTime.UtcNow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AdministrationException>(async () => await CreateUser(dispatcher, context, "New", grandchild.ResourceId));
        await Assert.ThrowsAsync<AdministrationException>(async () => await CreateUser(dispatcher, context, "New", long.MaxValue));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[UserAccount] SET [ManagerUserId] = [UserId] WHERE [UserId] = {root.ResourceId}",
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Identity].[UserAccount] SET [ManagerUserId] = {long.MaxValue} WHERE [UserId] = {root.ResourceId}",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentOppositeAssignments_CannotCreateCycle()
    {
        await using var provider = CreateProvider();
        await using var setup = provider.CreateAsyncScope();
        var dispatcher = setup.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var first = await CreateUser(dispatcher, context, "First");
        var second = await CreateUser(dispatcher, context, "Second");
        var results = await Task.WhenAll(
            TryAssign(provider, first.ResourceId, second.ResourceId, context),
            TryAssign(provider, second.ResourceId, first.ResourceId, context));
        Assert.Single(results, succeeded => succeeded);
        Assert.Single(results, succeeded => !succeeded);
    }

    private static async Task<bool> TryAssign(ServiceProvider provider, long userId, long managerId, AdministrationContext context)
    {
        await using var scope = provider.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(
                new UpdateUserProfileCommand(userId, "Updated", null, managerId, context),
                TestContext.Current.CancellationToken);
            return true;
        }
        catch (AdministrationException)
        {
            return false;
        }
    }

    private static ValueTask<AdministrationResult> CreateUser(IRequestDispatcher dispatcher,
        AdministrationContext context, string name, long? managerId = null) => dispatcher.Send(
        new CreateUserCommand($"EMP-{Guid.NewGuid():N}-X", name, context, null, managerId),
        TestContext.Current.CancellationToken);

    private static ServiceProvider CreateProvider()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to an isolated migrated test database.");
        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer, TestAuthorizer>();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        return services.BuildServiceProvider();
    }

    private sealed class TestAuthorizer : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
