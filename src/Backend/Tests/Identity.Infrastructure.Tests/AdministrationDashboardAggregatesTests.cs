using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class AdministrationDashboardAggregatesTests
{
    [Fact]
    public async Task GetDashboard_AggregatesTrendLockoutsMfaGapsAndExpiringSecrets()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server dashboard aggregates test.");
        }

        // A synthetic, hour-aligned "now" far from real traffic keeps the 24h trend window isolated.
        var generatedAt = new DateTime(2019, 6, 1, 12, 34, 0, DateTimeKind.Utc);
        var currentHour = new DateTime(2019, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var windowStart = currentHour.AddHours(-23);

        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer>(new TestAdministrationAuthorizer());
        services.AddIdentityApplication();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(generatedAt));
        services.AddIdentityPersistence(connectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IAdministrationDiscoveryStore>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new AdministrationContext(null, Guid.NewGuid());

        var before = await store.GetDashboard(5, generatedAt, cancellationToken);

        dbContext.AuthenticationAudits.Add(AuthenticationAudit.CreateAuthenticationEvent(
            AuthenticationAuditEventType.LoginFailed, false, "InvalidCredentials", Guid.NewGuid(),
            windowStart.AddHours(2).AddMinutes(10)));
        dbContext.AuthenticationAudits.Add(AuthenticationAudit.CreateAuthenticationEvent(
            AuthenticationAuditEventType.LoginSucceeded, true, null, Guid.NewGuid(),
            windowStart.AddHours(2).AddMinutes(20)));
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = await dispatcher.Send(new CreateUserCommand(
            $"E-{Guid.NewGuid():N}", "Dashboard Aggregates User", context), cancellationToken);
        var trackedUser = await dbContext.UserAccounts.SingleAsync(
            value => value.UserId == user.ResourceId, cancellationToken);
        trackedUser.RecordFailedVerification(generatedAt.AddMinutes(-5), 1, TimeSpan.FromHours(2));
        await dbContext.SaveChangesAsync(cancellationToken);

        var mfaUser = await dispatcher.Send(new CreateUserCommand(
            $"E-{Guid.NewGuid():N}", "Dashboard Aggregates MFA User", context), cancellationToken);
        var withoutMfaUser = await dispatcher.Send(new CreateUserCommand(
            $"E-{Guid.NewGuid():N}", "Dashboard Aggregates No MFA User", context), cancellationToken);
        var mfaMethod = UserMfaMethod.EnrollTotp(
            mfaUser.ResourceId, "Authenticator", [1, 2, 3, 4], "test-key", true, generatedAt);
        mfaMethod.Verify(generatedAt);
        dbContext.UserMfaMethods.Add(mfaMethod);
        await dbContext.SaveChangesAsync(cancellationToken);

        var application = await dispatcher.Send(new CreateApplicationCommand(
            $"app-{Guid.NewGuid():N}", "Dashboard Aggregates App", null,
            $"urn:identity:test:{Guid.NewGuid():N}", 15, 7, context), cancellationToken);
        await dispatcher.Send(new CreateApplicationClientCommand(
            application.ApplicationId!.Value, $"client-{Guid.NewGuid():N}", "Expiring Secret Client",
            ApplicationClientType.Confidential, new byte[32], generatedAt.AddDays(15), context),
            cancellationToken);

        var after = await store.GetDashboard(5, generatedAt, cancellationToken);

        // The audit log is append-only, so earlier test runs may already have rows in this
        // synthetic window; assert deltas rather than absolute counts to stay idempotent.
        Assert.Equal(24, after.AuthenticationTrend!.Count);
        Assert.Equal(windowStart, after.AuthenticationTrend[0].Hour);
        Assert.Equal(
            before.AuthenticationTrend![2].Succeeded + 1, after.AuthenticationTrend[2].Succeeded);
        Assert.Equal(
            before.AuthenticationTrend[2].Failed + 1, after.AuthenticationTrend[2].Failed);
        Assert.Equal(0, after.AuthenticationTrend[5].Succeeded);
        Assert.Equal(0, after.AuthenticationTrend[5].Failed);
        Assert.Equal(before.LockedOutUserCount + 1, after.LockedOutUserCount);
        // Two of the three new users (the locked-out one and withoutMfaUser) have no verified MFA method.
        Assert.Equal(before.UsersWithoutMfaCount + 2, after.UsersWithoutMfaCount);
        Assert.Equal(before.ExpiringClientSecretCount + 1, after.ExpiringClientSecretCount);
        _ = withoutMfaUser;
    }

    private sealed class TestAdministrationAuthorizer : IAdministrationAuthorizer
    {
        public ValueTask Authorize(
            AdministrationContext context,
            AdministrationAction action,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
