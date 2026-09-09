using System.Security.Cryptography;
using System.Text.Json;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Agents;
using Identity.Application.Messaging;
using Identity.Contracts.Agents;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class AgentControlRegistryTests
{
    [Fact]
    public async Task Signed_control_persists_serializes_commands_rejects_replay_and_cross_device_acknowledgements_and_honors_revocation()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrEmpty(connection)) Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to a disposable migrated IAM database.");
        var ct = TestContext.Current.CancellationToken;
        using var signer = RSA.Create(2048);
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection!);
        services.AddIdentitySecurity(new JwtSigningOptions("https://identity.test", "test", signer.ExportPkcs8PrivateKeyPem()),
            new SecurityProtectionOptions("test", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<IAdministrationAuthorizer, AllowAdministration>();
        await using var provider = services.BuildServiceProvider();
        await using var setup = provider.CreateAsyncScope();
        var user = await setup.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(new CreateUserCommand(
            "CONTROL-" + Guid.NewGuid().ToString("N"), "Control test administrator", new(null, Guid.NewGuid())), ct);
        var actor = new AdministrationContext(user.ResourceId, Guid.NewGuid());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var id = Guid.NewGuid(); var otherId = Guid.NewGuid();
        var registry = setup.ServiceProvider.GetRequiredService<IAgentRegistry>();
        var installed = await registry.Enroll(new(id, "CONTROL-PC", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())), actor, ct);
        await registry.Enroll(new(otherId, "OTHER-PC", Convert.ToBase64String(otherKey.ExportSubjectPublicKeyInfo())), actor, ct);
        async Task<T> InScope<T>(Func<IAgentControlRegistry, Task<T>> operation)
        {
            await using var scope = provider.CreateAsyncScope();
            return await operation(scope.ServiceProvider.GetRequiredService<IAgentControlRegistry>());
        }
        var sequence = 0;
        SignedMachineReport Proof(Guid installationId, ECDsa deviceKey, AgentUpdateAcknowledgement? ack = null, string supervisor = "1.0.1")
        {
            var report = new AgentControlReport(installationId, DateTimeOffset.UtcNow.AddMilliseconds(++sequence), "1.0.1", supervisor, ack);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
            return new(installationId, Convert.ToBase64String(bytes), Convert.ToBase64String(deviceKey.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        Assert.Null(await InScope(r => r.RequestUpdate(id, actor, ct))); // No supervisor heartbeat yet.
        Assert.NotNull(await InScope(r => r.Poll(Proof(id, key, supervisor: "1.0.0"), ct)));
        Assert.Null(await InScope(r => r.RequestUpdate(id, actor, ct))); // Older supervisor cannot accept commands.
        var heartbeat = Proof(id, key);
        Assert.NotNull(await InScope(r => r.Poll(heartbeat, ct)));
        Assert.Null(await InScope(r => r.Poll(heartbeat, ct))); // Persisted replay rejection across contexts.
        Assert.Null(await InScope(r => r.Poll(Proof(id, otherKey), ct)));
        var requests = await Task.WhenAll(InScope(r => r.RequestUpdate(id, actor, ct)), InScope(r => r.RequestUpdate(id, actor, ct)));
        Assert.Equal(requests[0]!.RequestId, requests[1]!.RequestId);
        var requestId = requests[0]!.RequestId;
        Assert.Equal("queued", (await InScope(r => r.Get(id, ct)))!.Update!.Status);
        // An acknowledgement cannot finish a request that has never been delivered.
        var delivery = await InScope(r => r.Poll(Proof(id, key, new(requestId, "updated")), ct));
        Assert.Equal(requestId, delivery!.Update!.RequestId);
        Assert.Equal("received", (await InScope(r => r.Get(id, ct)))!.Update!.Status);
        await InScope(r => r.Poll(Proof(otherId, otherKey, new(requestId, "updated")), ct));
        Assert.Equal("received", (await InScope(r => r.Get(id, ct)))!.Update!.Status);
        var completed = await InScope(r => r.Poll(Proof(id, key, new(requestId, "upToDate")), ct));
        Assert.Null(completed!.Update);
        Assert.Equal("upToDate", (await InScope(r => r.Get(id, ct)))!.Update!.Status);
        Assert.Equal(requestId, (await InScope(r => r.RequestUpdate(id, actor, ct)))!.RequestId); // Cooldown.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var request = await db.Set<AgentUpdateRequest>().SingleAsync(a => a.RequestId == requestId, ct);
            Assert.Equal(user.ResourceId, request.RequestedByUserId);
            Assert.Equal(actor.CorrelationId, request.CorrelationId);
            request.RequestedAt = DateTime.UtcNow.AddMinutes(-20); request.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            request.CompletedAt = null; request.Result = null;
            await db.SaveChangesAsync(ct);
        }
        Assert.Equal("expired", (await InScope(r => r.Get(id, ct)))!.Update!.Status);
        Assert.Null((await InScope(r => r.Poll(Proof(id, key), ct)))!.Update);
        Assert.NotEqual(requestId, (await InScope(r => r.RequestUpdate(id, actor, ct)))!.RequestId);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var device = await db.Devices.SingleAsync(d => d.DeviceId == installed.TerminalId, ct);
            device.Revoke(user.ResourceId, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
        }
        Assert.Null(await InScope(r => r.Poll(Proof(id, key), ct)));
        Assert.Null(await InScope(r => r.RequestUpdate(id, actor, ct)));
    }
    private sealed class AllowAdministration : IAdministrationAuthorizer
    { public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken ct) => ValueTask.CompletedTask; }
}
