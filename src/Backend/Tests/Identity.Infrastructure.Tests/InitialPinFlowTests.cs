using System.Security.Cryptography;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class InitialPinFlowTests
{
    [Fact]
    public async Task NewUser_ReceivesTemporaryPin_AndOwnIamLoginActivatesItExactlyOnce()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_INITIAL_PIN_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Requires an isolated, migrated PIN test database.");
        var target = new SqlConnectionStringBuilder(connection);
        Assert.Equal(@"(localdb)\MSSQLLocalDB", target.DataSource, ignoreCase: true);
        Assert.StartsWith("FIN_IAM_PinActivationTests_", target.InitialCatalog);
        using var rsa = RSA.Create(2048);
        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer, AllowAdministration>();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        services.AddIdentitySecurity(new JwtSigningOptions("https://identity.test", "pin-test", rsa.ExportPkcs8PrivateKeyPem()),
            new SecurityProtectionOptions("pin-test", new byte[32], new byte[32], new byte[32]));
        await using var provider = services.BuildServiceProvider();
        var ct = TestContext.Current.CancellationToken;
        async Task<T> Send<T>(IRequest<T> command)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(command, ct);
        }
        await using (var preflight = provider.CreateAsyncScope())
            Assert.Empty(await preflight.ServiceProvider.GetRequiredService<IdentityDbContext>().UserAccounts.AsNoTracking().ToArrayAsync(ct));
        var context = new AdministrationContext(null, Guid.NewGuid());
        var suffix = Guid.NewGuid().ToString("N");
        var employee = $"PIN-{suffix}-3275";
        var user = await Send(new CreateUserCommand(employee, "PIN fixture", context));
        context = context with { ActorUserId = user.ResourceId };
        await using var inspect = provider.CreateAsyncScope();
        var db = inspect.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = provider.GetRequiredService<IPinHasher>();
        var original = await db.UserCredentials.AsNoTracking().SingleAsync(x => x.UserId == user.ResourceId, ct);
        Assert.True(original.RequiresChange);
        Assert.True(hasher.Verify("3275", original));
        Assert.False(await db.UserApplications.AnyAsync(x => x.UserId == user.ResourceId, ct));
        await Send(new UpdateUserProfileCommand(user.ResourceId, "Renamed PIN fixture", null, null, context));
        Assert.Equal(original.SecretHash, (await db.UserCredentials.AsNoTracking().SingleAsync(x => x.UserId == user.ResourceId, ct)).SecretHash);

        var app = await Send(new CreateApplicationCommand($"pin-app-{suffix}", "PIN fixture", null, $"urn:pin:{suffix}", 15, 7, context));
        var appId = app.ResourceId;
        await Send(new GrantUserApplicationCommand(user.ResourceId, appId, context));
        var module = await Send(new CreateModuleCommand(appId, "terminal", "Terminal", null, null, 1, false, context));
        var role = await Send(new CreateRoleCommand(appId, "terminal-user", "Terminal user", null, false, context));
        await Send(new AssignRoleCommand(user.ResourceId, appId, role.ResourceId, context));
        foreach (var code in new[] { "pts.shopfloor.operate", "pts.production.read" })
        {
            var capability = await Send(new CreateCapabilityCommand(appId, module.ResourceId, code, code, null, context));
            await Send(new SetUserPermissionOverrideCommand(user.ResourceId, appId, capability.ResourceId, PermissionEffect.Allow, "Isolated test", null, context));
        }
        var browserId = $"pin-web-{suffix}";
        var serviceId = $"pin-service-{suffix}";
        const string serviceSecret = "isolated-service-secret-only";
        await Send(new CreateApplicationClientCommand(appId, browserId, "Browser", ApplicationClientType.Public, null, null, context));
        await Send(new CreateApplicationClientCommand(appId, serviceId, "Backend", ApplicationClientType.Service,
            provider.GetRequiredService<ISecretHasher>().Hash(serviceSecret), null, context));
        var terminal = await Send(new RegisterDeviceCommand(user.ResourceId, "Isolated PIN terminal", "Terminal", RandomNumberGenerator.GetBytes(32), null, null, context));
        await Send(new TrustDeviceCommand(terminal.ResourceId, context));
        const string password = "Isolated-login-only-Password42!";
        await Send(new SetPasswordCommand(user.ResourceId, password, null, context));
        var login = await Send(new LoginCommand(employee, password, browserId, null, null, Guid.NewGuid()));
        Assert.True(login.Succeeded);
        var request = new CurrentTerminalVerificationCommand(terminal.ResourceId, login.AccessToken!, serviceId, serviceSecret, Guid.NewGuid());
        Assert.Equal(TerminalVerificationFailureCode.PinChangeRequired, (await Send(request)).FailureCode);
        var pinRequest = new TerminalVerificationCommand(terminal.ResourceId, employee, "3275", serviceId, serviceSecret, Guid.NewGuid());
        Assert.Equal(TerminalVerificationFailureCode.PinChangeRequired, (await Send(pinRequest)).FailureCode);
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await Send(request with { NewPin = "3275" })).FailureCode);

        Assert.True((await Send(request with { NewPin = "2468" })).Succeeded);
        Assert.True((await Send(request with { NewPin = "2468" })).Succeeded); // Lost response retry.
        Assert.True((await Send(request)).Succeeded); // Same IAM token; no second password login.
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await Send(request with { NewPin = "1357" })).FailureCode);
        Assert.Equal(TerminalVerificationFailureCode.InvalidCredentials, (await Send(pinRequest)).FailureCode);
        Assert.True((await Send(pinRequest with { Pin = "2468" })).Succeeded);
        var pins = await db.UserCredentials.AsNoTracking().Where(x => x.UserId == user.ResourceId && x.CredentialType == CredentialType.Pin).ToArrayAsync(ct);
        Assert.Equal(2, pins.Length);
        var active = Assert.Single(pins, x => x.RevokedAt == null);
        Assert.False(active.RequiresChange);
        Assert.True(hasher.Verify("2468", active));
        Assert.False(hasher.Verify("3275", active));
        Assert.All(await db.AuthenticationAudits.AsNoTracking().Where(x => x.UserId == user.ResourceId
            && (x.EventType == "CredentialChanged" || x.EventType.StartsWith("TerminalVerification"))).ToArrayAsync(ct),
            a => Assert.Null(a.EventDataJson));
    }

    private sealed class AllowAdministration : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
