using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Domain.Enums;
using Identity.Infrastructure;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Tests;

public sealed class AuthenticationFlowTests
{
    [Fact]
    public void SecurityServices_HashAndVerifySecretsWithoutReturningInput()
    {
        using var rsa = RSA.Create(2048);
        using var provider = CreateServices(
            ModelOnlyConnectionString,
            rsa.ExportPkcs8PrivateKeyPem()).BuildServiceProvider();
        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var secretHasher = provider.GetRequiredService<ISecretHasher>();
        var tokenGenerator = provider.GetRequiredService<IRandomTokenGenerator>();
        const string password = "Correct-Horse-Battery-Staple-42";

        var passwordHash = passwordHasher.Hash(password);
        var secretHash = secretHasher.Hash("client-secret-value");
        var randomToken = tokenGenerator.Generate();

        Assert.Equal("PBKDF2-SHA256", passwordHash.Algorithm);
        Assert.Equal(32, passwordHash.Salt.Length);
        Assert.Equal(32, passwordHash.Hash.Length);
        Assert.True(secretHasher.Verify("client-secret-value", secretHash));
        Assert.False(secretHasher.Verify("different-secret", secretHash));
        Assert.DoesNotContain(password, Convert.ToHexString(passwordHash.Hash), StringComparison.Ordinal);
        Assert.True(randomToken.Length >= 43);
    }

    [Fact]
    public void JwtIssuer_RejectsInvalidIssuerAndWeakSigningKey()
    {
        using var weakRsa = RSA.Create(1024);
        var weakKeyProvider = new ServiceCollection()
            .AddIdentitySecurity(new JwtSigningOptions(
                "https://identity.test",
                "weak-key",
                weakRsa.ExportPkcs8PrivateKeyPem()))
            .BuildServiceProvider();
        var invalidIssuerProvider = new ServiceCollection()
            .AddIdentitySecurity(new JwtSigningOptions(
                "not-an-absolute-issuer",
                "test-key",
                CreatePrivateKey()))
            .BuildServiceProvider();

        Assert.Throws<ArgumentException>(weakKeyProvider.GetRequiredService<IAccessTokenIssuer>);
        Assert.Throws<ArgumentException>(invalidIssuerProvider.GetRequiredService<IAccessTokenIssuer>);
        weakKeyProvider.Dispose();
        invalidIssuerProvider.Dispose();
    }

    [Fact]
    public async Task TokenFlow_IssuesRotatesRejectsReplayAndPublishesVerifiableKey()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server token flow.");
        }

        using var rsa = RSA.Create(2048);
        var timeProvider = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        var services = CreateServices(
            connectionString,
            rsa.ExportPkcs8PrivateKeyPem(),
            timeProvider);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        const string clientSecret = "client-secret-with-enough-entropy";
        const string password = "Correct-Horse-Battery-Staple-42";

        var application = await dispatcher.Send(new CreateApplicationCommand(
            $"token-app-{Guid.NewGuid():N}",
            "Token Test",
            null,
            $"urn:identity:token-test:{Guid.NewGuid():N}",
            15,
            7,
            context), TestContext.Current.CancellationToken);
        var user = await dispatcher.Send(new CreateUserCommand(
            $"TOKEN-{Guid.NewGuid():N}",
            "Token User",
            context), TestContext.Current.CancellationToken);
        var actorContext = context with { ActorUserId = user.ResourceId };
        var clientId = $"token-client-{Guid.NewGuid():N}";
        var client = await dispatcher.Send(new CreateApplicationClientCommand(
            application.ApplicationId!.Value,
            clientId,
            "Token Client",
            ApplicationClientType.Confidential,
            secretHasher.Hash(clientSecret),
            null,
            actorContext), TestContext.Current.CancellationToken);
        var module = await dispatcher.Send(new CreateModuleCommand(
            application.ApplicationId.Value,
            $"token-module-{Guid.NewGuid():N}",
            "Token Module",
            null,
            null,
            1,
            false,
            actorContext), TestContext.Current.CancellationToken);
        var capabilityCode = $"token.read-{Guid.NewGuid():N}";
        var capability = await dispatcher.Send(new CreateCapabilityCommand(
            application.ApplicationId.Value,
            module.ResourceId,
            capabilityCode,
            "Read Token Data",
            null,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            actorContext), TestContext.Current.CancellationToken);
        var role = await dispatcher.Send(new CreateRoleCommand(
            application.ApplicationId.Value,
            $"token-role-{Guid.NewGuid():N}",
            "Token Role",
            null,
            false,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new AssignRoleCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            role.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantRolePermissionCommand(
            application.ApplicationId.Value,
            role.ResourceId,
            capability.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new SetPasswordCommand(
            user.ResourceId,
            password,
            null,
            actorContext), TestContext.Current.CancellationToken);

        var userResource = await dispatcher.Send(new GetAdministrationResourceQuery(
            AdministrationResourceKind.User,
            user.ResourceId), TestContext.Current.CancellationToken);
        var employeeCode = userResource.Code!;
        var rejectedLogin = await dispatcher.Send(new LoginCommand(
            employeeCode,
            "Wrong-password-value",
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.False(rejectedLogin.Succeeded);
        Assert.Equal(AuthenticationFailureCode.InvalidCredentials, rejectedLogin.FailureCode);
        var rejectedClient = await dispatcher.Send(new LoginCommand(
            employeeCode,
            password,
            clientId,
            "wrong-client-secret",
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.InvalidClient, rejectedClient.FailureCode);
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            var rejected = await dispatcher.Send(new LoginCommand(
                employeeCode,
                "Wrong-password-value",
                clientId,
                clientSecret,
                null,
                Guid.NewGuid()), TestContext.Current.CancellationToken);
            Assert.Equal(AuthenticationFailureCode.InvalidCredentials, rejected.FailureCode);
        }
        var lockedLogin = await dispatcher.Send(new LoginCommand(
            employeeCode,
            password,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.AccountLocked, lockedLogin.FailureCode);
        timeProvider.Advance(TimeSpan.FromMinutes(16));
        var login = await dispatcher.Send(new LoginCommand(
            employeeCode,
            password,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.True(login.Succeeded);
        Assert.NotNull(login.AccessToken);
        Assert.NotNull(login.RefreshToken);
        ValidateAccessToken(
            login.AccessToken,
            application.ApplicationId.Value,
            clientId,
            capabilityCode,
            rsa,
            timeProvider);

        var rotated = await dispatcher.Send(new RefreshSessionCommand(
            login.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(rotated.Succeeded);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var replay = await dispatcher.Send(new RefreshSessionCommand(
            login.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.RefreshTokenReused, replay.FailureCode);

        var revokedFamily = await dispatcher.Send(new RefreshSessionCommand(
            rotated.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.RefreshTokenReused, revokedFamily.FailureCode);

        var securityVersionSession = await dispatcher.Send(new LoginCommand(
            employeeCode,
            password,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        const string replacementPassword = "Replacement-Horse-Battery-Staple-84";
        await dispatcher.Send(new SetPasswordCommand(
            user.ResourceId,
            replacementPassword,
            null,
            actorContext), TestContext.Current.CancellationToken);
        var staleSecurityVersion = await dispatcher.Send(new RefreshSessionCommand(
            securityVersionSession.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.SessionVersionStale, staleSecurityVersion.FailureCode);

        var secondLogin = await dispatcher.Send(new LoginCommand(
            employeeCode,
            replacementPassword,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        await dispatcher.Send(new SetUserPermissionOverrideCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            capability.ResourceId,
            PermissionEffect.Deny,
            "Invalidate an already-issued refresh token",
            null,
            actorContext), TestContext.Current.CancellationToken);
        var stale = await dispatcher.Send(new RefreshSessionCommand(
            secondLogin.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.SessionVersionStale, stale.FailureCode);

        var logoutSession = await dispatcher.Send(new LoginCommand(
            employeeCode,
            replacementPassword,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        var logout = await dispatcher.Send(new LogoutCommand(
            logoutSession.RefreshToken!,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        var loggedOutRefresh = await dispatcher.Send(new RefreshSessionCommand(
            logoutSession.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(logout.Succeeded);
        Assert.Equal(AuthenticationFailureCode.RefreshTokenReused, loggedOutRefresh.FailureCode);

        var expiringSession = await dispatcher.Send(new LoginCommand(
            employeeCode,
            replacementPassword,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromDays(8));
        var expired = await dispatcher.Send(new RefreshSessionCommand(
            expiringSession.RefreshToken!,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.RefreshTokenExpired, expired.FailureCode);

        await dispatcher.Send(new ChangeAdministrationResourceStateCommand(
            AdministrationResourceKind.User,
            user.ResourceId,
            null,
            null,
            false,
            actorContext), TestContext.Current.CancellationToken);
        var disabledUser = await dispatcher.Send(new LoginCommand(
            employeeCode,
            replacementPassword,
            clientId,
            clientSecret,
            null,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.InvalidCredentials, disabledUser.FailureCode);

        var metadata = await dispatcher.Send(
            new GetSigningMetadataQuery(),
            TestContext.Current.CancellationToken);
        Assert.Equal("https://identity.test", metadata.Issuer);
        Assert.Equal("test-key", metadata.JsonWebKey.KeyId);
    }

    [Fact]
    public async Task TerminalVerification_RequiresTrustedTerminalAndLocksRepeatedPinFailures()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server terminal flow.");
        }

        using var rsa = RSA.Create(2048);
        await using var provider = CreateServices(
            connectionString,
            rsa.ExportPkcs8PrivateKeyPem()).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        const string clientSecret = "terminal-client-secret-with-enough-entropy";
        const string pin = "4682";

        var application = await dispatcher.Send(new CreateApplicationCommand(
            $"terminal-app-{Guid.NewGuid():N}",
            "Terminal Test",
            null,
            $"urn:identity:terminal-test:{Guid.NewGuid():N}",
            15,
            7,
            context), TestContext.Current.CancellationToken);
        var employeeCode = $"TERMINAL-{Guid.NewGuid():N}";
        var user = await dispatcher.Send(new CreateUserCommand(
            employeeCode,
            "Terminal Operator",
            context), TestContext.Current.CancellationToken);
        var actorContext = context with { ActorUserId = user.ResourceId };
        var clientId = $"terminal-client-{Guid.NewGuid():N}";
        await dispatcher.Send(new CreateApplicationClientCommand(
            application.ApplicationId!.Value,
            clientId,
            "Terminal Service Client",
            ApplicationClientType.Service,
            secretHasher.Hash(clientSecret),
            null,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new SetPinCommand(
            user.ResourceId,
            pin,
            actorContext), TestContext.Current.CancellationToken);
        var terminal = await dispatcher.Send(new RegisterDeviceCommand(
            user.ResourceId,
            "Shared shop-floor terminal",
            "Terminal",
            RandomNumberGenerator.GetBytes(32),
            null,
            null,
            actorContext), TestContext.Current.CancellationToken);

        var untrusted = await VerifyTerminal(
            dispatcher, terminal.ResourceId, employeeCode, pin, clientId, clientSecret);
        Assert.Equal(TerminalVerificationFailureCode.TerminalNotTrusted, untrusted.FailureCode);

        await dispatcher.Send(new TrustDeviceCommand(
            terminal.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        var verified = await VerifyTerminal(
            dispatcher, terminal.ResourceId, employeeCode, pin, clientId, clientSecret);
        Assert.True(verified.Succeeded);
        Assert.Equal(user.ResourceId, verified.UserId);
        Assert.Equal("Terminal Operator", verified.DisplayName);
        Assert.NotNull(verified.CapabilityCodes);
        Assert.Empty(verified.CapabilityCodes);

        // PTS approval must use capabilities from this verified employee, not the
        // terminal connection's bearer identity or a name selected in the UI.
        var module = await dispatcher.Send(new CreateModuleCommand(
            application.ApplicationId.Value, "shopfloor", "Shop floor", null, null, 1, false, actorContext),
            TestContext.Current.CancellationToken);
        var capability = await dispatcher.Send(new CreateCapabilityCommand(
            application.ApplicationId.Value, module.ResourceId, "pts.shopfloor.supervise", "Supervise", null, actorContext),
            TestContext.Current.CancellationToken);
        var role = await dispatcher.Send(new CreateRoleCommand(
            application.ApplicationId.Value, "supervisor", "Supervisor", null, false, actorContext),
            TestContext.Current.CancellationToken);
        await dispatcher.Send(new AssignRoleCommand(user.ResourceId, application.ApplicationId.Value,
            role.ResourceId, actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantRolePermissionCommand(application.ApplicationId.Value,
            role.ResourceId, capability.ResourceId, actorContext), TestContext.Current.CancellationToken);
        var supervisor = await VerifyTerminal(dispatcher, terminal.ResourceId, employeeCode, pin, clientId, clientSecret);
        Assert.Equal(["pts.shopfloor.supervise"], supervisor.CapabilityCodes);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rejected = await VerifyTerminal(
                dispatcher, terminal.ResourceId, employeeCode, "0000", clientId, clientSecret);
            Assert.Equal(TerminalVerificationFailureCode.InvalidCredentials, rejected.FailureCode);
        }

        var locked = await VerifyTerminal(
            dispatcher, terminal.ResourceId, employeeCode, pin, clientId, clientSecret);
        Assert.Equal(TerminalVerificationFailureCode.LockedOut, locked.FailureCode);
    }

    private static ValueTask<TerminalVerificationResult> VerifyTerminal(
        IRequestDispatcher dispatcher,
        long terminalId,
        string employeeCode,
        string pin,
        string clientId,
        string clientSecret) => dispatcher.Send(new TerminalVerificationCommand(
            terminalId,
            employeeCode,
            pin,
            clientId,
            clientSecret,
            Guid.NewGuid()), TestContext.Current.CancellationToken);

    private static ServiceCollection CreateServices(
        string connectionString,
        string privateKeyPem,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer, AllowAdministrationAuthorizer>();
        services.AddIdentityApplication();
        if (timeProvider is not null)
        {
            services.AddSingleton<TimeProvider>(timeProvider);
        }
        services.AddIdentityPersistence(connectionString);
        services.AddIdentitySecurity(new JwtSigningOptions(
            "https://identity.test",
            "test-key",
            privateKeyPem),
            CreateProtectionOptions());
        return services;
    }

    private static SecurityProtectionOptions CreateProtectionOptions() => new(
        "test-protection-key",
        Enumerable.Repeat((byte)1, 32).ToArray(),
        Enumerable.Repeat((byte)2, 32).ToArray(),
        Enumerable.Repeat((byte)3, 32).ToArray());

    private static void ValidateAccessToken(
        string? token,
        long applicationId,
        string clientId,
        string capabilityCode,
        RSA rsa,
        TimeProvider timeProvider)
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://identity.test",
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                return notBefore <= now && expires > now;
            },
        }, out var validatedToken);

        Assert.Equal(SecurityAlgorithms.RsaSha256, ((JwtSecurityToken)validatedToken).Header.Alg);
        Assert.Equal(applicationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            principal.FindFirst("application_id")?.Value);
        Assert.Equal(clientId, principal.FindFirst("client_id")?.Value);
        Assert.Contains(principal.FindAll("capability"), claim => claim.Value == capabilityCode);
    }

    private const string ModelOnlyConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Identity_ModelOnly;Integrated Security=true";

    private static string CreatePrivateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private sealed class AllowAdministrationAuthorizer : IAdministrationAuthorizer
    {
        public ValueTask Authorize(
            AdministrationContext context,
            AdministrationAction action,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialValue) : TimeProvider
    {
        private DateTimeOffset value = initialValue;

        public override DateTimeOffset GetUtcNow() => value;

        public void Advance(TimeSpan duration) => value = value.Add(duration);
    }
}
