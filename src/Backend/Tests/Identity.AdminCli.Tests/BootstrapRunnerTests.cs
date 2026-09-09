using System.Security.Cryptography;
using System.Text;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;

namespace Identity.AdminCli.Tests;

public sealed class BootstrapRunnerTests
{
    [Fact]
    public async Task Run_RejectsWeakSecretsBeforeCreatingAnyResource()
    {
        var dispatcher = new RecordingDispatcher();
        var runner = new BootstrapRunner(dispatcher, new TestSecretHasher());
        var options = new BootstrapOptions(
            "ADMIN-001",
            "Initial Administrator",
            "iam-administration",
            "Identity Administration",
            "identity-bootstrap-client",
            "urn:identity:administration");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await runner.Run(
            options,
            "short",
            "also-short",
            TestContext.Current.CancellationToken));

        Assert.Empty(dispatcher.Requests);
    }

    [Fact]
    public async Task Run_CreatesCompleteAdministrationChainWithoutReturningSecrets()
    {
        var dispatcher = new RecordingDispatcher();
        var runner = new BootstrapRunner(dispatcher, new TestSecretHasher());
        const string password = "Bootstrap-Horse-Battery-Staple-42";
        const string clientSecret = "bootstrap-client-secret-with-entropy";

        var result = await runner.Run(
            new BootstrapOptions(
                "ADMIN-001",
                "Initial Administrator",
                "iam-administration",
                "Identity Administration",
                "identity-bootstrap-client",
                "urn:identity:administration",
                "administrator@example.com"),
            password,
            clientSecret,
            TestContext.Current.CancellationToken);

        Assert.Equal(new BootstrapResult(1, 2, 3, 6, 5), result);
        Assert.Equal("administrator@example.com", Assert.IsType<CreateUserCommand>(dispatcher.Requests[1]).Email);
        Assert.Collection(
            dispatcher.Requests,
            request => Assert.IsType<CreateApplicationCommand>(request),
            request => Assert.IsType<CreateUserCommand>(request),
            request => Assert.IsType<CreateApplicationClientCommand>(request),
            request => Assert.IsType<CreateModuleCommand>(request),
            request => Assert.IsType<CreateCapabilityCommand>(request),
            request => Assert.IsType<GrantUserApplicationCommand>(request),
            request => Assert.IsType<CreateRoleCommand>(request),
            request => Assert.IsType<AssignRoleCommand>(request),
            request => Assert.IsType<GrantRolePermissionCommand>(request),
            request => Assert.IsType<SetPasswordCommand>(request));
        var client = Assert.IsType<CreateApplicationClientCommand>(dispatcher.Requests[2]);
        Assert.NotEqual(Encoding.UTF8.GetBytes(clientSecret), client.ClientSecretHash);
        var serializedResult = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain(password, serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, serializedResult, StringComparison.Ordinal);
    }

    private sealed class RecordingDispatcher : IRequestDispatcher
    {
        public List<object> Requests { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            object result = request switch
            {
                CreateApplicationCommand => new AdministrationResult("Application", 1, null, 1, null),
                CreateUserCommand => new AdministrationResult("User", 2, 2, null, null),
                CreateApplicationClientCommand => new AdministrationResult(
                    "ApplicationClient", 3, null, 1, null),
                CreateModuleCommand => new AdministrationResult("Module", 4, null, 1, null),
                CreateCapabilityCommand => new AdministrationResult("Capability", 5, null, 1, null),
                GrantUserApplicationCommand => new AdministrationResult(
                    "UserApplication", 1, 2, 1, 1),
                CreateRoleCommand => new AdministrationResult("Role", 6, null, 1, null),
                AssignRoleCommand => new AdministrationResult("UserRole", 7, 2, 1, 2),
                GrantRolePermissionCommand => new AdministrationResult(
                    "RolePermission", 8, null, 1, null),
                SetPasswordCommand => OperationResult.Success,
                _ => throw new NotSupportedException(request.GetType().Name),
            };
            return ValueTask.FromResult((TResponse)result);
        }
    }

    private sealed class TestSecretHasher : ISecretHasher
    {
        public byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        public bool Verify(string secret, byte[] expectedHash) =>
            CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
    }
}
