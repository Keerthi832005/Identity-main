using System.Security.Cryptography;
using System.Text.Json;
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

public sealed class DefaultAdministratorSeedTests
{
    [Fact]
    public void Profile_SelectsRequestedEmployeeAndIamOnly()
    {
        var profile = DefaultAdministratorSeedRunner.Profile;
        Assert.Equal("INDE03275", profile.EmployeeCode);
        Assert.Equal("Siddeswaran S", profile.DisplayName);
        Assert.Equal("Siddeswaran.S@fujitec.co.in", profile.Email);
        Assert.Equal("iam-administration", profile.ApplicationCode);
        Assert.Equal("urn:identity:administration", profile.Audience);
    }

    [Theory]
    [InlineData("--password", "unsafe")]
    [InlineData("--employee-code", "ANOTHER")]
    [InlineData("--apply", "--apply")]
    public async Task Cli_RejectsSecretsAndIdentityOverrides(string first, string second)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, await AdminCliHost.Run(["seed-default-administrator", first, second], output, error, TestContext.Current.CancellationToken));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain("unsafe", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SqlSeed_PreviewsRollsBackCreatesAdminAndPreservesCredentialsOnReplay()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_ADMIN_SEED_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Requires an empty disposable SQL database.");
        Assert.StartsWith("FIN_IAM_OrgMgmtTests_", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        var token = TestContext.Current.CancellationToken;
        using var rsa = RSA.Create(2048);
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        services.AddIdentitySecurity(new JwtSigningOptions("https://seed-test.example", "seed-test", rsa.ExportPkcs8PrivateKeyPem()),
            new SecurityProtectionOptions("seed-test", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<IAdministrationAuthorizer, BootstrapAdministrationAuthorizer>();
        services.AddScoped<ApplicationProvisioningRunner>();
        services.AddScoped<AdministrationWebProvisioningRunner>();
        services.AddScoped<DefaultAdministratorSeedRunner>();
        services.AddScoped<BootstrapRunner>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await db.UserAccounts.AnyAsync(token));
        var seed = scope.ServiceProvider.GetRequiredService<DefaultAdministratorSeedRunner>();
        // The public CLI preview must work without requiring a bootstrap password/key bundle.
        var priorConnection = Environment.GetEnvironmentVariable("IDENTITY_DATABASE_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("IDENTITY_DATABASE_CONNECTION", connection);
            using var previewOutput = new StringWriter();
            using var previewError = new StringWriter();
            Assert.Equal(0, await AdminCliHost.Run(["seed-default-administrator"], previewOutput, previewError, token));
            Assert.Contains("would-create", previewOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal("", previewError.ToString());
        }
        finally { Environment.SetEnvironmentVariable("IDENTITY_DATABASE_CONNECTION", priorConnection); }
        const string password = "Disposable-seed-password-only-42!";
        Task<BootstrapResult> Bootstrap(CancellationToken ct) => scope.ServiceProvider.GetRequiredService<BootstrapRunner>()
            .Run(DefaultAdministratorSeedRunner.Profile, password, "Disposable-client-secret-only-42!", ct);
        Task<BootstrapResult> MustNotBootstrap(CancellationToken _) => throw new InvalidOperationException("Replay must not bootstrap again.");

        Assert.Equal("would-create", (await seed.Run(false, MustNotBootstrap, token)).State);
        Assert.False(await db.UserAccounts.AnyAsync(token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => seed.Run(true, async ct =>
        {
            await Bootstrap(ct);
            throw new InvalidOperationException("Synthetic failure after bootstrap.");
        }, token));
        Assert.False(await db.UserAccounts.AnyAsync(token));
        Assert.False(await db.Applications.AnyAsync(token));
        Assert.False(await db.UserCredentials.AnyAsync(token));

        var created = await seed.Run(true, Bootstrap, token);
        Assert.True(created.Created);
        Assert.Equal("identity-administrator", created.Role);
        var user = await db.UserAccounts.AsNoTracking().SingleAsync(token);
        Assert.Equal("INDE03275", user.EmployeeCode);
        Assert.Equal("Siddeswaran.S@fujitec.co.in", user.Email);
        Assert.Null(user.ManagerUserId);
        Assert.Single(await db.Applications.ToArrayAsync(token));
        Assert.Single(await db.ApplicationClients.Where(row => row.ClientId == "identity-admin-web").ToArrayAsync(token));
        var credentials = JsonSerializer.Serialize(await db.UserCredentials.AsNoTracking().ToArrayAsync(token));
        var audits = await db.AuthenticationAudits.CountAsync(token);
        var replay = await seed.Run(true, MustNotBootstrap, token);
        Assert.False(replay.Created);
        Assert.Equal("already-seeded", replay.State);
        Assert.Equal(created.UserId, replay.UserId);
        Assert.Equal(credentials, JsonSerializer.Serialize(await db.UserCredentials.AsNoTracking().ToArrayAsync(token)));
        Assert.Equal(audits, await db.AuthenticationAudits.CountAsync(token));
        var login = await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(new LoginCommand(
            "INDE03275", password, "identity-admin-web", null, null, Guid.NewGuid()), token);
        Assert.True(login.Succeeded);
        var application = await db.Applications.SingleAsync(token);
        var authorization = await scope.ServiceProvider.GetRequiredService<IAdministrationStore>().GetEffectiveAuthorization(
            user.UserId, application.ApplicationId, DateTime.UtcNow, token);
        Assert.Contains("iam.admin", authorization!.CapabilityCodes);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[UserApplication] SET IsActive=0 WHERE UserId={user.UserId}", token);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => seed.Run(true, MustNotBootstrap, token));
        Assert.False(await db.UserApplications.AnyAsync(row => row.UserId == user.UserId && row.IsActive, token));
        Assert.Equal(credentials, JsonSerializer.Serialize(await db.UserCredentials.AsNoTracking().ToArrayAsync(token)));
    }
}
