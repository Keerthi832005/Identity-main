using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Domain.Enums;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.AdminCli.Tests;

public sealed class ApplicationProvisioningIntegrationTests
{
    [Fact]
    public async Task BootstrapThenWebProvisioning_PreservesLegacyDescriptionsAndRejectsContractDrift()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Assert.Skip("Requires isolated SQL.");
        }

        Assert.StartsWith("FIN_IAM_OrgMgmtTests_",
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        using var rsa = RSA.Create(2048);
        var suffix = Guid.NewGuid().ToString("N");
        var employee = "BOOT-" + suffix;
        var bootstrapServices = CreateProvisioningServices(
            connection,
            1,
            "iam-administration",
            "iam.admin",
            rsa.ExportPkcs8PrivateKeyPem());
        bootstrapServices.AddSingleton<IAdministrationAuthorizer, AllowAdministrationAuthorizer>();
        await using var bootstrapProvider = bootstrapServices.BuildServiceProvider();
        await using var bootstrapScope = bootstrapProvider.CreateAsyncScope();
        var bootstrap = new BootstrapRunner(
            bootstrapScope.ServiceProvider.GetRequiredService<IRequestDispatcher>(),
            bootstrapScope.ServiceProvider.GetRequiredService<ISecretHasher>());
        var created = await bootstrap.Run(new BootstrapOptions(
            employee,
            "Bootstrap test",
            "iam-administration",
            "Identity Administration",
            "bootstrap-" + suffix,
            "urn:identity:administration"),
            "Test-password-only-42!", "Test-client-secret-only-42!", TestContext.Current.CancellationToken);

        var services = CreateProvisioningServices(
            connection,
            created.AdministratorUserId,
            "iam-administration",
            "iam.admin",
            rsa.ExportPkcs8PrivateKeyPem());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var runner = new AdministrationWebProvisioningRunner(
            scope.ServiceProvider.GetRequiredService<ApplicationProvisioningRunner>(),
            scope.ServiceProvider.GetRequiredService<IRequestDispatcher>(),
            scope.ServiceProvider.GetRequiredService<IAdministrationStore>(),
            db);
        var first = await runner.Run(new(employee), TestContext.Current.CancellationToken);
        var replay = await runner.Run(new(employee), TestContext.Current.CancellationToken);
        Assert.Equal(first, replay);
        Assert.Equal(created.ApplicationId, first.ApplicationId);
        Assert.Equal(created.AdministratorUserId, first.UserId);
        Assert.Single(await db.ApplicationClients.Where(row => row.ClientId == "identity-admin-web")
            .ToListAsync(TestContext.Current.CancellationToken));
        var application = await db.Applications.AsNoTracking().SingleAsync(
            row => row.ApplicationId == created.ApplicationId, TestContext.Current.CancellationToken);
        Assert.Equal("Administration application created by the secure bootstrap command.", application.Description);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[Application] SET Description=N'Unexpected drift' WHERE ApplicationId={created.ApplicationId}", TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ApplicationProvisioningException>(() => runner.Run(new(employee), TestContext.Current.CancellationToken));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[Application] SET Description={application.Description},TokenAudience=N'wrong-audience' WHERE ApplicationId={created.ApplicationId}", TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ApplicationProvisioningException>(() => runner.Run(new(employee), TestContext.Current.CancellationToken));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[Application] SET TokenAudience={application.TokenAudience} WHERE ApplicationId={created.ApplicationId}", TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var login = await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(new LoginCommand(
            employee,
            "Test-password-only-42!",
            "identity-admin-web",
            null,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(login.Succeeded);
        Assert.Contains(ValidateToken(login.AccessToken, "urn:identity:administration", rsa).FindAll("capability"), claim => claim.Value == "iam.admin");
    }

    [Fact]
    public async Task Provisioning_IsIdempotentAuthorizedAndIssuesConsumablePublicClientToken()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run application provisioning tests.");
        }

        var suffix = Guid.NewGuid().ToString("N");
        var administrationCode = $"iam-admin-{suffix}";
        var actorUserId = await CreateAdministrationActor(
            connectionString,
            administrationCode,
            suffix);
        using var rsa = RSA.Create(2048);
        var manifest = CreateManifest(suffix);
        var useCapability = $"application.use.{suffix}";
        var manageCapability = $"application.manage.{suffix}";
        var services = CreateProvisioningServices(
            connectionString,
            actorUserId,
            administrationCode,
            $"iam.admin-{suffix}",
            rsa.ExportPkcs8PrivateKeyPem());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ApplicationProvisioningRunner>();

        var first = await runner.Run(
            manifest,
            actorUserId,
            TestContext.Current.CancellationToken);
        var replay = await runner.Run(
            manifest,
            actorUserId,
            TestContext.Current.CancellationToken);

        Assert.True(first.Application.Created);
        Assert.All(first.PublicClients, resource => Assert.True(resource.Created));
        Assert.All(first.Modules, resource => Assert.True(resource.Created));
        Assert.All(first.Capabilities, resource => Assert.True(resource.Created));
        Assert.All(first.Roles, resource => Assert.True(resource.Created));
        Assert.Equal(2, first.CreatedRolePermissions);
        Assert.False(replay.Application.Created);
        Assert.All(replay.PublicClients, resource => Assert.False(resource.Created));
        Assert.All(replay.Modules, resource => Assert.False(resource.Created));
        Assert.All(replay.Capabilities, resource => Assert.False(resource.Created));
        Assert.All(replay.Roles, resource => Assert.False(resource.Created));
        Assert.Equal(0, replay.CreatedRolePermissions);
        Assert.Equal(2, replay.ExistingRolePermissions);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(actorUserId, Guid.NewGuid());
        var user = await dispatcher.Send(new CreateUserCommand(
            $"PTS-{suffix}",
            "Provisioned Application User",
            context), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            first.Application.ResourceId,
            context), TestContext.Current.CancellationToken);
        await dispatcher.Send(new AssignRoleCommand(
            user.ResourceId,
            first.Application.ResourceId,
            first.Roles.Single().ResourceId,
            context), TestContext.Current.CancellationToken);
        const string password = "Provisioned-Horse-Battery-Staple-42";
        await dispatcher.Send(new SetPasswordCommand(
            user.ResourceId,
            password,
            null,
            context), TestContext.Current.CancellationToken);

        var login = await dispatcher.Send(new LoginCommand(
            $"PTS-{suffix}",
            password,
            manifest.PublicClients.Single().ClientId,
            null,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.True(login.Succeeded);
        var principal = ValidateToken(login.AccessToken, manifest.Audience, rsa);
        Assert.Equal(manifest.PublicClients.Single().ClientId, principal.FindFirst("client_id")?.Value);
        Assert.Contains(principal.FindAll("capability"), claim => claim.Value == useCapability);
        Assert.Contains(principal.FindAll("capability"), claim => claim.Value == manageCapability);

        var capability = first.Capabilities.Single(value => value.Code == manageCapability);
        await dispatcher.Send(new SetUserPermissionOverrideCommand(
            user.ResourceId,
            first.Application.ResourceId,
            capability.ResourceId,
            PermissionEffect.Deny,
            "Validate explicit user removal from a role grant.",
            null,
            context), TestContext.Current.CancellationToken);
        var restrictedLogin = await dispatcher.Send(new LoginCommand(
            $"PTS-{suffix}",
            password,
            manifest.PublicClients.Single().ClientId,
            null,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        var restricted = ValidateToken(restrictedLogin.AccessToken, manifest.Audience, rsa);

        Assert.Contains(restricted.FindAll("capability"), claim => claim.Value == useCapability);
        Assert.DoesNotContain(restricted.FindAll("capability"), claim => claim.Value == manageCapability);
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await dbContext.ApplicationClients.CountAsync(
            value => value.ApplicationId == first.Application.ResourceId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await dbContext.ApplicationModules.CountAsync(
            value => value.ApplicationId == first.Application.ResourceId,
            TestContext.Current.CancellationToken));
        Assert.Equal(2, await dbContext.ModuleCapabilities.CountAsync(
            value => value.ApplicationId == first.Application.ResourceId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await dbContext.Roles.CountAsync(
            value => value.ApplicationId == first.Application.ResourceId,
            TestContext.Current.CancellationToken));
        Assert.Equal(2, await dbContext.RolePermissions.CountAsync(
            value => value.ApplicationId == first.Application.ResourceId && value.RevokedAt == null,
            TestContext.Current.CancellationToken));
    }

    private static async Task<long> CreateAdministrationActor(
        string connectionString,
        string applicationCode,
        string suffix)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer, AllowAdministrationAuthorizer>();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var initialContext = new AdministrationContext(null, Guid.NewGuid());
        var application = await dispatcher.Send(new CreateApplicationCommand(
            applicationCode,
            "Provisioning Administration",
            null,
            $"urn:identity:provisioning:{suffix}",
            15,
            7,
            initialContext), TestContext.Current.CancellationToken);
        var user = await dispatcher.Send(new CreateUserCommand(
            $"ADMIN-{suffix}",
            "Provisioning Administrator",
            initialContext), TestContext.Current.CancellationToken);
        var context = initialContext with { ActorUserId = user.ResourceId };
        var module = await dispatcher.Send(new CreateModuleCommand(
            application.ApplicationId!.Value,
            $"administration-{suffix}",
            "Administration",
            null,
            null,
            0,
            true,
            context), TestContext.Current.CancellationToken);
        var capability = await dispatcher.Send(new CreateCapabilityCommand(
            application.ApplicationId.Value,
            module.ResourceId,
            $"iam.admin-{suffix}",
            "Identity administrator",
            null,
            context), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            context), TestContext.Current.CancellationToken);
        var role = await dispatcher.Send(new CreateRoleCommand(
            application.ApplicationId.Value,
            $"admin-{suffix[..8]}",
            "Identity Administrator",
            null,
            true,
            context), TestContext.Current.CancellationToken);
        await dispatcher.Send(new AssignRoleCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            role.ResourceId,
            context), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantRolePermissionCommand(
            application.ApplicationId.Value,
            role.ResourceId,
            capability.ResourceId,
            context), TestContext.Current.CancellationToken);
        return user.ResourceId;
    }

    private static ServiceCollection CreateProvisioningServices(
        string connectionString,
        long actorUserId,
        string administrationApplicationCode,
        string requiredCapability,
        string privateKeyPem)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ProvisioningActor(
            actorUserId,
            administrationApplicationCode,
            requiredCapability));
        services.AddScoped<IAdministrationAuthorizer, ProvisioningAdministrationAuthorizer>();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        services.AddIdentitySecurity(
            new JwtSigningOptions("https://identity.test", "provisioning-test", privateKeyPem),
            new SecurityProtectionOptions(
                "provisioning-test",
                Enumerable.Repeat((byte)1, 32).ToArray(),
                Enumerable.Repeat((byte)2, 32).ToArray(),
                Enumerable.Repeat((byte)3, 32).ToArray()));
        services.AddScoped<ApplicationProvisioningRunner>();
        return services;
    }

    private static ApplicationProvisioningManifest CreateManifest(string suffix) => new(
        $"provisioned-{suffix}",
        "Provisioned Application",
        "Application provisioning integration test.",
        $"provisioned-api-{suffix}",
        15,
        7,
        [new PublicClientManifest($"provisioned-web-{suffix}", "Provisioned Web")],
        [new ApplicationModuleManifest(
            $"application-{suffix}",
            "Application",
            null,
            0,
            true,
            [
                new ModuleCapabilityManifest(
                    $"application.use.{suffix}",
                    "Use application",
                    null),
                new ModuleCapabilityManifest(
                    $"application.manage.{suffix}",
                    "Manage application",
                    null),
            ])],
        [new ApplicationRoleManifest(
            $"operator-{suffix[..8]}",
            "Operator",
            null,
            true,
            [$"application.use.{suffix}", $"application.manage.{suffix}"])]);

    private static System.Security.Claims.ClaimsPrincipal ValidateToken(
        string? token,
        string audience,
        RSA rsa)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://identity.test",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ClockSkew = TimeSpan.Zero,
        }, out _);
    }

    private sealed class AllowAdministrationAuthorizer : IAdministrationAuthorizer
    {
        public ValueTask Authorize(
            AdministrationContext context,
            AdministrationAction action,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
