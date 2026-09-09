using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Application.Security;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class MfaSecurityTests
{
    [Fact]
    public void SecurityPrimitives_ProtectHashAndSerializeOnlyApprovedData()
    {
        using var rsa = RSA.Create(2048);
        using var provider = CreateServices(
            ModelOnlyConnectionString,
            rsa.ExportPkcs8PrivateKeyPem()).BuildServiceProvider();
        var protector = provider.GetRequiredService<ISecretProtector>();
        var challengeHasher = provider.GetRequiredService<IChallengeHasher>();
        var identifierHasher = provider.GetRequiredService<IIdentifierHasher>();
        var serializer = provider.GetRequiredService<IAuditPayloadSerializer>();
        var totp = provider.GetRequiredService<ITotpService>();
        var plaintext = Encoding.UTF8.GetBytes("mfa-enrollment-secret");

        var protectedSecret = protector.Protect(plaintext);
        var tampered = protectedSecret.Ciphertext.ToArray();
        tampered[^1] ^= 0x01;
        var challengeId = Guid.NewGuid();
        var challengeHash = challengeHasher.Hash(challengeId);
        var normalizedIdentifier = identifierHasher.Hash(" employee-42 ");
        var approvedJson = serializer.Serialize(new MfaMethodAuditData(42, "Totp", "Verified"));
        var vectorSecret = Encoding.ASCII.GetBytes("12345678901234567890");
        var vectorTime = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;

        Assert.Equal(plaintext, protector.Unprotect(protectedSecret.Ciphertext, protectedSecret.KeyId));
        Assert.DoesNotContain(Convert.ToHexString(plaintext),
            Convert.ToHexString(protectedSecret.Ciphertext), StringComparison.OrdinalIgnoreCase);
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tampered, protectedSecret.KeyId));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(
            protectedSecret.Ciphertext, "unknown-key"));
        Assert.True(challengeHasher.Verify(challengeId, challengeHash, challengeHasher.KeyId));
        Assert.False(challengeHasher.Verify(Guid.NewGuid(), challengeHash, challengeHasher.KeyId));
        Assert.False(challengeHasher.Verify(challengeId, challengeHash, "unknown-key"));
        Assert.Equal(normalizedIdentifier, identifierHasher.Hash("EMPLOYEE-42"));
        Assert.NotEqual(normalizedIdentifier, identifierHasher.Hash("employee-43"));
        Assert.Contains("\"action\":\"Verified\"", approvedJson, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(
            new PasswordAuditData("never-persist-this")));
        Assert.True(totp.Verify(vectorSecret, "287082", vectorTime));
        Assert.True(totp.Verify(vectorSecret, "287082", vectorTime.AddSeconds(30)));
        Assert.False(totp.Verify(vectorSecret, "287082", vectorTime.AddSeconds(60)));
        Assert.False(totp.Verify(vectorSecret, "not-a-code", vectorTime));
    }

    [Fact]
    public async Task MfaFlow_EnforcesAttemptsExpiryReplayTrustedDevicesAndSafeAudit()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server MFA flow.");
        }

        using var rsa = RSA.Create(2048);
        var timeProvider = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        var services = CreateServices(connectionString, rsa.ExportPkcs8PrivateKeyPem(), timeProvider);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        const string clientSecret = "mfa-client-secret-with-enough-entropy";
        const string password = "Mfa-Horse-Battery-Staple-42";

        var application = await dispatcher.Send(new CreateApplicationCommand(
            $"mfa-app-{Guid.NewGuid():N}",
            "MFA Test",
            null,
            $"urn:identity:mfa-test:{Guid.NewGuid():N}",
            15,
            7,
            context), TestContext.Current.CancellationToken);
        var user = await dispatcher.Send(new CreateUserCommand(
            $"MFA-{Guid.NewGuid():N}",
            "MFA User",
            context), TestContext.Current.CancellationToken);
        var actorContext = context with { ActorUserId = user.ResourceId };
        var clientId = $"mfa-client-{Guid.NewGuid():N}";
        await dispatcher.Send(new CreateApplicationClientCommand(
            application.ApplicationId!.Value,
            clientId,
            "MFA Client",
            ApplicationClientType.Confidential,
            secretHasher.Hash(clientSecret),
            null,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            actorContext), TestContext.Current.CancellationToken);
        await dispatcher.Send(new SetPasswordCommand(
            user.ResourceId,
            password,
            null,
            actorContext), TestContext.Current.CancellationToken);
        var device = await dispatcher.Send(new RegisterDeviceCommand(
            user.ResourceId,
            "MFA workstation",
            "Desktop",
            SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"))),
            null,
            null,
            actorContext), TestContext.Current.CancellationToken);
        var userResource = await dispatcher.Send(new GetAdministrationResourceQuery(
            AdministrationResourceKind.User,
            user.ResourceId), TestContext.Current.CancellationToken);
        var employeeCode = userResource.Code!;

        var enrollment = await dispatcher.Send(new EnrollTotpCommand(
            user.ResourceId,
            "Primary authenticator",
            true,
            actorContext), TestContext.Current.CancellationToken);
        var storedMethod = await dbContext.UserMfaMethods.AsNoTracking().SingleAsync(
            value => value.UserMfaMethodId == enrollment.UserMfaMethodId,
            TestContext.Current.CancellationToken);
        var decodedSecret = DecodeBase32(enrollment.Secret);
        Assert.NotEqual(decodedSecret, storedMethod.SecretEncrypted);
        Assert.Equal("test-protection-key", storedMethod.EncryptionKeyId);

        var rejectedEnrollment = await dispatcher.Send(new VerifyTotpEnrollmentCommand(
            enrollment.UserMfaMethodId,
            "000000",
            actorContext), TestContext.Current.CancellationToken);
        var currentCode = ComputeTotp(decodedSecret, timeProvider.GetUtcNow());
        var verifiedEnrollment = await dispatcher.Send(new VerifyTotpEnrollmentCommand(
            enrollment.UserMfaMethodId,
            currentCode,
            actorContext), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.MfaInvalid, rejectedEnrollment.FailureCode);
        Assert.True(verifiedEnrollment.Succeeded);

        var limitedLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        Assert.NotNull(limitedLogin.MfaChallengeId);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var attemptLogin = attempt == 1
                ? limitedLogin
                : await Login(
                    dispatcher, employeeCode, password, clientId, clientSecret, null);
            Assert.NotNull(attemptLogin.MfaChallengeId);
            var rejected = await dispatcher.Send(new CompleteMfaLoginCommand(
                attemptLogin.MfaChallengeId!.Value,
                "999999",
                Guid.NewGuid()), TestContext.Current.CancellationToken);
            Assert.Equal(
                attempt == 5
                    ? AuthenticationFailureCode.AccountLocked
                    : AuthenticationFailureCode.MfaInvalid,
                rejected.FailureCode);
        }

        var lockedChallenge = await dispatcher.Send(new CompleteMfaLoginCommand(
            limitedLogin.MfaChallengeId.Value,
            currentCode,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.AccountLocked, lockedChallenge.FailureCode);

        var lockedLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        Assert.Equal(AuthenticationFailureCode.AccountLocked, lockedLogin.FailureCode);
        timeProvider.Advance(TimeSpan.FromMinutes(16));
        currentCode = ComputeTotp(decodedSecret, timeProvider.GetUtcNow());
        var successfulLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        var completed = await dispatcher.Send(new CompleteMfaLoginCommand(
            successfulLogin.MfaChallengeId!.Value,
            currentCode,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        var replay = await dispatcher.Send(new CompleteMfaLoginCommand(
            successfulLogin.MfaChallengeId.Value,
            currentCode,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(completed.Succeeded);
        Assert.NotNull(completed.RefreshToken);
        Assert.Equal(AuthenticationFailureCode.MfaExpired, replay.FailureCode);

        var replayLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        var replayedTimeStep = await dispatcher.Send(new CompleteMfaLoginCommand(
            replayLogin.MfaChallengeId!.Value,
            currentCode,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.MfaInvalid, replayedTimeStep.FailureCode);
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        currentCode = ComputeTotp(decodedSecret, timeProvider.GetUtcNow());
        var nextTimeStep = await dispatcher.Send(new CompleteMfaLoginCommand(
            replayLogin.MfaChallengeId.Value,
            currentCode,
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(nextTimeStep.Succeeded);

        var expiringLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await dispatcher.Send(new CompleteMfaLoginCommand(
            expiringLogin.MfaChallengeId!.Value,
            ComputeTotp(decodedSecret, timeProvider.GetUtcNow()),
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.MfaExpired, expired.FailureCode);

        var untrustedDeviceLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, device.ResourceId);
        Assert.NotNull(untrustedDeviceLogin.MfaChallengeId);
        await dispatcher.Send(new TrustDeviceCommand(
            device.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        var trustedDeviceLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, device.ResourceId);
        Assert.True(trustedDeviceLogin.Succeeded);
        Assert.Null(trustedDeviceLogin.MfaChallengeId);
        timeProvider.Advance(TimeSpan.FromDays(31));
        var expiredTrustLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, device.ResourceId);
        Assert.NotNull(expiredTrustLogin.MfaChallengeId);

        await dispatcher.Send(new ChangeAdministrationResourceStateCommand(
            AdministrationResourceKind.Device,
            device.ResourceId,
            null,
            null,
            false,
            actorContext), TestContext.Current.CancellationToken);
        var revokedDeviceLogin = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, device.ResourceId);
        var revokedDeviceChallenge = await dispatcher.Send(new CompleteMfaLoginCommand(
            expiredTrustLogin.MfaChallengeId!.Value,
            ComputeTotp(decodedSecret, timeProvider.GetUtcNow()),
            Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationFailureCode.AccessDenied, revokedDeviceLogin.FailureCode);
        Assert.Equal(AuthenticationFailureCode.AccessDenied, revokedDeviceChallenge.FailureCode);

        await dispatcher.Send(new RevokeMfaMethodCommand(
            enrollment.UserMfaMethodId,
            actorContext), TestContext.Current.CancellationToken);
        var loginAfterMfaRevocation = await Login(
            dispatcher, employeeCode, password, clientId, clientSecret, null);
        Assert.True(loginAfterMfaRevocation.Succeeded);

        var audits = await dbContext.AuthenticationAudits.AsNoTracking()
            .Where(value => value.UserId == user.ResourceId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var auditJson = string.Join('|', audits.Select(value => value.EventDataJson));
        Assert.DoesNotContain(enrollment.Secret, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(password, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(currentCode, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, auditJson, StringComparison.Ordinal);
        Assert.All(
            audits.Where(value => value.EventType is "LoginSucceeded" or "MfaChallengeCreated"),
            value => Assert.NotNull(value.LoginIdentifierHash));
        Assert.Contains(audits, value => value.EventType == "MfaMethodVerificationRejected");
        Assert.Contains(audits, value => value.EventType == "DeviceTrusted");
        Assert.Contains(audits, value => value.EventType == "MfaChallengeRejected");
    }

    private static ValueTask<AuthenticationResult> Login(
        IRequestDispatcher dispatcher,
        string employeeCode,
        string password,
        string clientId,
        string clientSecret,
        long? deviceId) => dispatcher.Send(new LoginCommand(
            employeeCode,
            password,
            clientId,
            clientSecret,
            deviceId,
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
            services.AddSingleton(timeProvider);
        }

        services.AddIdentityPersistence(connectionString);
        services.AddIdentitySecurity(
            new JwtSigningOptions("https://identity.test", "test-signing-key", privateKeyPem),
            new SecurityProtectionOptions(
                "test-protection-key",
                Enumerable.Repeat((byte)1, 32).ToArray(),
                Enumerable.Repeat((byte)2, 32).ToArray(),
                Enumerable.Repeat((byte)3, 32).ToArray()));
        return services;
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var character in value)
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new FormatException("Invalid Base32 value.");
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return output.ToArray();
    }

    private static string ComputeTotp(byte[] secret, DateTimeOffset timestamp)
    {
        var counter = timestamp.ToUnixTimeSeconds() / 30;
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        var hash = HMACSHA1.HashData(secret, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private const string ModelOnlyConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Identity_ModelOnly;Integrated Security=true";

    private sealed record PasswordAuditData(string Password) : IAuditPayload;

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
