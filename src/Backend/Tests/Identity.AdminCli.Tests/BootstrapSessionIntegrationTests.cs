using System.Security.Cryptography;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.AdminCli.Tests;

public sealed class BootstrapSessionIntegrationTests
{
    [Fact]
    public async Task Bootstrap_IsAtomicFirstAdministratorOnlyAndPreservesOrdinaryBranchRequirement()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_BOOTSTRAP_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Requires a new disposable bootstrap SQL database.");
        Assert.Matches("^FIN_IAM_OrgMgmtTests_Bootstrap_[a-f0-9]{32}$",
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        var token = TestContext.Current.CancellationToken;
        using var rsa = RSA.Create(2048);
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        services.AddIdentitySecurity(new JwtSigningOptions("https://bootstrap-test.example", "bootstrap-test", rsa.ExportPkcs8PrivateKeyPem()),
            new SecurityProtectionOptions("bootstrap-test", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<IAdministrationAuthorizer, BootstrapAdministrationAuthorizer>();
        services.AddScoped<IBootstrapSession, BootstrapSession>();
        services.AddScoped<BootstrapRunner>();
        services.AddScoped<ApplicationProvisioningRunner>();
        services.AddScoped<AdministrationWebProvisioningRunner>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var session = scope.ServiceProvider.GetRequiredService<IBootstrapSession>();
        var hasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var runner = scope.ServiceProvider.GetRequiredService<BootstrapRunner>();
        var options = new BootstrapOptions("TEST04316", "Bootstrap administrator", "iam-administration",
            "Identity Administration", "bootstrap-test-client", "urn:identity:administration", "bootstrap@example.invalid");
        const string password = "Disposable-bootstrap-password-only-42!";
        const string clientSecret = "Disposable-bootstrap-client-only-42!";
        Assert.False(await db.UserAccounts.AnyAsync(token));
        Assert.False(await db.Applications.AnyAsync(token));

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CreateAdministrator(
            options, new(null, Guid.NewGuid()), token));
        var contextError = await Assert.ThrowsAsync<InvalidOperationException>(() => session.Execute(async (context, ct) =>
        {
            await session.CreateAdministrator(options, context with { ActorUserId = 123 }, ct);
            throw new InvalidOperationException("Unexpected trusted context acceptance.");
        }, token));
        Assert.Equal("Initial administrator creation requires the active trusted bootstrap transaction.", contextError.Message);

        var failingRunner = new BootstrapRunner(new FailAfterPasswordDispatcher(dispatcher), hasher, session);
        await Assert.ThrowsAsync<InjectedBootstrapFailure>(() => failingRunner.Run(options, password, clientSecret, token));
        Assert.False(await db.UserAccounts.AnyAsync(token));
        Assert.False(await db.Applications.AnyAsync(token));
        Assert.False(await db.UserCredentials.AnyAsync(token));
        Assert.False(await db.AuthenticationAudits.AnyAsync(token));
        Assert.False(await db.Roles.AnyAsync(token));
        Assert.False(await db.ModuleCapabilities.AnyAsync(token));

        var created = await runner.Run(options, password, clientSecret, token);
        var user = await db.UserAccounts.AsNoTracking().SingleAsync(token);
        Assert.Equal(created.AdministratorUserId, user.UserId);
        Assert.Null(user.BranchId);
        Assert.Null(user.DepartmentId);
        Assert.Null(user.ManagerUserId);
        Assert.Equal("bootstrap@example.invalid", user.Email);
        Assert.Equal(2, await db.UserCredentials.CountAsync(token));
        Assert.Equal(1, await db.AuthenticationAudits.CountAsync(row => row.EventType == "UserCreated", token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.Run(options, password, clientSecret, token));
        var branchError = await Assert.ThrowsAsync<AdministrationException>(async () =>
            await dispatcher.Send(new CreateUserCommand("ORDINARY-USER", "Ordinary user",
                new(created.AdministratorUserId, Guid.NewGuid())), token));
        Assert.Equal("Select a branch before creating a user.", branchError.Message);
        Assert.Equal(1, await db.UserAccounts.CountAsync(token));

        await scope.ServiceProvider.GetRequiredService<AdministrationWebProvisioningRunner>().Run(new(options.EmployeeCode), token);
        var login = await dispatcher.Send(new LoginCommand(options.EmployeeCode, password,
            "identity-admin-web", null, null, Guid.NewGuid()), token);
        Assert.True(login.Succeeded);
        var access = await scope.ServiceProvider.GetRequiredService<IAdministrationStore>().GetEffectiveAuthorization(
            user.UserId, created.ApplicationId, TimeProvider.System.GetUtcNow().UtcDateTime, token);
        Assert.Contains("iam.admin", access!.CapabilityCodes);
    }

    private sealed class InjectedBootstrapFailure : Exception;

    private sealed class FailAfterPasswordDispatcher(IRequestDispatcher inner) : IRequestDispatcher
    {
        public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var result = await inner.Send(request, cancellationToken);
            if (request is SetPasswordCommand) throw new InjectedBootstrapFailure();
            return result;
        }
    }
}
