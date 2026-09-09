using System.Reflection;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Tests;

public sealed class CurrentTerminalVerificationTests
{
    [Fact]
    public async Task SignedInEmployee_IsVerifiedWithoutRequiringAPinEntry()
    {
        var fixture = new Fixture();
        var result = await fixture.Run();
        Assert.True(result.Succeeded);
        Assert.Equal(42, result.UserId);
        Assert.Equal("INDE03275", result.EmployeeCode);
        var audit = Assert.Single(fixture.Audits);
        Assert.True(audit.Succeeded);
        Assert.Equal(77, audit.DeviceId);
        Assert.Null(audit.EventDataJson);
    }

    [Fact]
    public async Task TemporaryPin_BlocksWorkspaceUntilOwnerChoosesNewPin_AndRetriesAreSafe()
    {
        var fixture = new Fixture();
        var initial = fixture.Pin = Fixture.Credential("3275", requiresChange: true);
        Assert.Equal(TerminalVerificationFailureCode.PinChangeRequired, (await fixture.Run()).FailureCode);
        var changed = await fixture.Run("2468");
        Assert.True(changed.Succeeded);
        Assert.NotNull(initial.RevokedAt);
        Assert.False(fixture.Pin!.RequiresChange);
        Assert.Equal(1, fixture.User.SecurityVersion); // Keep password/MFA session intact.
        Assert.Single(fixture.Audits, a => a.EventType == AuthenticationAuditEventType.CredentialChanged.ToString());
        Assert.True((await fixture.Run()).Succeeded);
        Assert.True((await fixture.Run("2468")).Succeeded); // Lost-response retry; no second replacement.
        Assert.Single(fixture.AddedPins);
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await fixture.Run("1357")).FailureCode);
        Assert.Single(fixture.AddedPins);
        Assert.All(fixture.Audits, a => Assert.Null(a.EventDataJson));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12ab")]
    [InlineData("１２３４")]
    [InlineData("3275")]
    public async Task InvalidOrDefaultReplacement_DoesNotChangeCredential(string newPin)
    {
        var fixture = new Fixture();
        var original = fixture.Pin = Fixture.Credential("3275", requiresChange: true);
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await fixture.Run(newPin)).FailureCode);
        Assert.Null(original.RevokedAt);
        Assert.Empty(fixture.AddedPins);
    }

    [Fact]
    public async Task MissingOrEstablishedPin_CannotBeResetThroughInitialActivation()
    {
        var fixture = new Fixture();
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await fixture.Run("2468")).FailureCode);
        fixture.Pin = Fixture.Credential("1357", requiresChange: false);
        Assert.Equal(TerminalVerificationFailureCode.InvalidNewPin, (await fixture.Run("2468")).FailureCode);
        Assert.Null(fixture.Pin.RevokedAt);
        Assert.Empty(fixture.AddedPins);
    }

    [Theory]
    [InlineData("service-secret", TerminalVerificationFailureCode.InvalidClient)]
    [InlineData("service-revoked", TerminalVerificationFailureCode.InvalidClient)]
    [InlineData("application-disabled", TerminalVerificationFailureCode.InvalidClient)]
    [InlineData("token-invalid", TerminalVerificationFailureCode.InvalidCredentials)]
    [InlineData("wrong-application", TerminalVerificationFailureCode.InvalidCredentials)]
    [InlineData("wrong-browser-app", TerminalVerificationFailureCode.InvalidClient)]
    [InlineData("browser-revoked", TerminalVerificationFailureCode.InvalidClient)]
    [InlineData("terminal-revoked", TerminalVerificationFailureCode.TerminalNotTrusted)]
    [InlineData("terminal-expired", TerminalVerificationFailureCode.TerminalNotTrusted)]
    [InlineData("user-disabled", TerminalVerificationFailureCode.InvalidCredentials)]
    [InlineData("security-version", TerminalVerificationFailureCode.InvalidCredentials)]
    [InlineData("user-locked", TerminalVerificationFailureCode.LockedOut)]
    [InlineData("access-revoked", TerminalVerificationFailureCode.AccessDenied)]
    [InlineData("authorization-version", TerminalVerificationFailureCode.AccessDenied)]
    [InlineData("permission-denied", TerminalVerificationFailureCode.AccessDenied)]
    public async Task RejectsInvalidOrStaleIdentity(string scenario, TerminalVerificationFailureCode expected)
    {
        var fixture = new Fixture();
        switch (scenario)
        {
            case "service-secret": fixture.SecretValid = false; break;
            case "service-revoked": fixture.Service.Revoke(fixture.Now); break;
            case "application-disabled": fixture.App.SetActive(false, fixture.Now); break;
            case "token-invalid": fixture.Identity = null; break;
            case "wrong-application": fixture.Identity = fixture.Identity! with { ApplicationId = 8 }; break;
            case "wrong-browser-app": fixture.Browser = ApplicationClient.Create(8, "pts-web", "Browser", ApplicationClientType.Public, null, fixture.Now, null); break;
            case "browser-revoked": fixture.Browser.Revoke(fixture.Now); break;
            case "terminal-revoked": fixture.Device.Revoke(null, fixture.Now); break;
            case "terminal-expired": fixture.Device.Trust(fixture.Now.AddDays(-2), TimeSpan.FromDays(1)); break;
            case "user-disabled": fixture.User.SetActive(false, fixture.Now); break;
            case "security-version": fixture.User.InvalidateSecurity(fixture.Now); break;
            case "user-locked": fixture.User.RecordFailedVerification(fixture.Now, 1, TimeSpan.FromMinutes(5)); break;
            case "access-revoked": fixture.Access.Revoke(null, fixture.Now); break;
            case "authorization-version": fixture.Permissions = fixture.Permissions with { AuthorizationVersion = 2 }; break;
            case "permission-denied": fixture.Permissions = fixture.Permissions with { CapabilityCodes = ["pts.production.read"] }; break;
        }
        var result = await fixture.Run();
        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.FailureCode);
        Assert.Null(result.UserId);
        Assert.False(Assert.Single(fixture.Audits).Succeeded);
        fixture.Audits.Clear();
        fixture.Pin = Fixture.Credential("3275", requiresChange: true);
        Assert.Equal(expected, (await fixture.Run("2468")).FailureCode);
        Assert.Null(fixture.Pin.RevokedAt);
        Assert.Empty(fixture.AddedPins);
    }

    private sealed class Fixture
    {
        public DateTime Now { get; } = DateTime.UtcNow;
        public RegisteredApplication App { get; }
        public ApplicationClient Service { get; }
        public ApplicationClient Browser { get; set; }
        public Device Device { get; }
        public UserAccount User { get; }
        public UserApplicationAccess Access { get; }
        public TerminalTokenIdentity? Identity = new(42, 7, "pts-web", 1, 1);
        public EffectiveAuthorization Permissions = new(1, ["pts.shopfloor.operate", "pts.production.read"]);
        public bool SecretValid = true;
        public List<AuthenticationAudit> Audits { get; } = [];
        public UserCredential? Pin;
        public List<UserCredential> AddedPins { get; } = [];
        public static UserCredential Credential(string pin, bool requiresChange) => UserCredential.CreatePin(
            42, "Test-only", 100000, new byte[32], Hash(pin), DateTime.UtcNow, null, requiresChange);
        private static byte[] Hash(string pin) => System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pin));
        public Fixture()
        {
            App = RegisteredApplication.Create("production-tracking", "PTS", null, "pts-api", 15, 7, Now);
            SetId(App, "ApplicationId", 7);
            Service = ApplicationClient.Create(7, "pts-service", "Backend", ApplicationClientType.Service, new byte[32], Now, null);
            Browser = ApplicationClient.Create(7, "pts-web", "Browser", ApplicationClientType.Public, null, Now, null);
            Device = Device.Create(42, "Fixture", "Terminal", new byte[32], null, null, Now);
            Device.Trust(Now, TimeSpan.FromDays(1));
            User = UserAccount.Create("INDE03275", "Test employee", Now);
            SetId(User, "UserId", 42);
            Access = UserApplicationAccess.Create(42, 7, null, Now);
        }
        public async Task<TerminalVerificationResult> Run(string? newPin = null)
        {
            var store = Stub<IAuthenticationStore>((method, args) => method.Name switch
            {
                "FindClientByClientId" => Task.FromResult<ApplicationClient?>((string)args[0]! == "pts-service" ? Service : Browser),
                "FindApplication" => ValueTask.FromResult<RegisteredApplication?>(App),
                "FindDevice" => ValueTask.FromResult<Device?>(Device),
                "FindUser" => ValueTask.FromResult<UserAccount?>((long)args[0]! == 42 ? User : null),
                "FindUserApplication" => ValueTask.FromResult<UserApplicationAccess?>(Access),
                "FindCurrentPin" => Task.FromResult(Pin),
                "Add" => AddAudit(args[0]),
                _ => throw new InvalidOperationException($"Unexpected store operation: {method.Name}"),
            });
            var admin = Stub<IAdministrationStore>((method, _) => method.Name == "GetEffectiveAuthorization"
                ? Task.FromResult<EffectiveAuthorization?>(Permissions) : throw new InvalidOperationException(method.Name));
            var secrets = Stub<ISecretHasher>((_, _) => SecretValid);
            var tokens = Stub<ITerminalAccessTokenValidator>((_, args) =>
            {
                Assert.Equal("fixture-bearer", args[0]);
                Assert.Equal("pts-api", args[1]);
                return Identity;
            });
            var unit = Stub<IUnitOfWork>((_, _) => Task.FromResult(1));
            var pins = Stub<IPinHasher>((method, args) => method.Name == "Hash"
                ? new PasswordHash("Test-only", 100000, new byte[32], Hash((string)args[0]!))
                : args[1] is UserCredential credential && credential.SecretHash.SequenceEqual(Hash((string)args[0]!)));
            var handler = new CurrentTerminalVerificationHandler(store, admin, secrets, tokens, unit, new Transactions(), TimeProvider.System, pins);
            return await handler.Handle(new(77, "fixture-bearer", "pts-service", "fixture-secret", Guid.NewGuid(), newPin), TestContext.Current.CancellationToken);
        }
        private object? AddAudit(object? value)
        {
            if (value is UserCredential pin) { AddedPins.Add(pin); Pin = pin; }
            else Audits.Add(Assert.IsType<AuthenticationAudit>(value));
            return null;
        }
        private static void SetId(object entity, string name, long id) => entity.GetType().GetProperty(name)!.SetValue(entity, id);
    }
    private sealed class Transactions : ITransactionRunner
    {
        public Task<T> Execute<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }
    private static T Stub<T>(Func<MethodInfo, object?[], object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, StrictProxy>();
        ((StrictProxy)(object)proxy).Call = call;
        return proxy;
    }
    public class StrictProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args ?? []);
    }
}
