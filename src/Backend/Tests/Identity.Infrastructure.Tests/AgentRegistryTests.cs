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

public sealed class AgentRegistryTests
{
    [Fact]
    public async Task Enrollment_is_idempotent_and_reports_require_the_device_key_and_cannot_revive_revocation()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrEmpty(connection)) Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to a disposable migrated IAM database.");
        using var signing = RSA.Create(2048);
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection!);
        services.AddIdentitySecurity(new JwtSigningOptions("https://identity.test", "test", signing.ExportPkcs8PrivateKeyPem()),
            new SecurityProtectionOptions("test", RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<IAdministrationAuthorizer, AllowTestAdministration>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var user = await dispatcher.Send(new CreateUserCommand("AGENT-" + Guid.NewGuid().ToString("N"), "Agent test administrator", context), TestContext.Current.CancellationToken);
        context = context with { ActorUserId = user.ResourceId };
        var registry = scope.ServiceProvider.GetRequiredService<IAgentRegistry>();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var enrollment = new AgentEnrollment(Guid.NewGuid(), "TEST-HOST", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        var first = await registry.Enroll(enrollment, context, TestContext.Current.CancellationToken);
        Assert.True(first.Trusted);
        Assert.Equal(first, await registry.Enroll(enrollment, context, TestContext.Current.CancellationToken));
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await Assert.ThrowsAsync<ArgumentException>(() => registry.Enroll(enrollment with { PublicKey = Convert.ToBase64String(wrongKey.ExportSubjectPublicKeyInfo()) }, context, TestContext.Current.CancellationToken));
        SignedMachineReport Report(DateTimeOffset capturedAt)
        {
            var report = new MachineInventory(enrollment.InstallationId, capturedAt, "RENAMED-HOST", "1.0.0", "Windows", "X64",
                new("Maker", "Model", "Serial", "CPU", 8, 1024, []),
                [new("TestApp", "1", "Test", new DateOnly(2026, 8, 31), 1048576, "64-bit")], []);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
            return new(enrollment.InstallationId, Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        var signed = Report(DateTimeOffset.UtcNow);
        Assert.True(await registry.Report(signed, TestContext.Current.CancellationToken));
        Assert.False(await registry.Report(signed, TestContext.Current.CancellationToken));
        Assert.False(await registry.Report(signed with { Signature = Convert.ToBase64String(new byte[64]) }, TestContext.Current.CancellationToken));
        var details = await registry.Get(enrollment.InstallationId, TestContext.Current.CancellationToken);
        Assert.Equal("RENAMED-HOST", details!.Machine.Hostname);
        Assert.Single(details.Inventory!.Software);
        Assert.Equal(new DateOnly(2026, 8, 31), details.Inventory.Software[0].InstalledOn);
        Assert.Equal(1048576, details.Inventory.Software[0].EstimatedSizeBytes);
        Assert.Equal("64-bit", details.Inventory.Software[0].RegistryView);
        var search = await registry.Search("RENAMED-HOST", 0, 50, TestContext.Current.CancellationToken);
        Assert.Contains(search.Items, a => a.InstallationId == enrollment.InstallationId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var device = await db.Devices.SingleAsync(d => d.DeviceId == first.TerminalId, TestContext.Current.CancellationToken);
        device.Revoke(user.ResourceId, DateTime.UtcNow);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await registry.Report(Report(DateTimeOffset.UtcNow.AddSeconds(1)), TestContext.Current.CancellationToken));
        Assert.False((await registry.Enroll(enrollment, context, TestContext.Current.CancellationToken)).Trusted);
        Assert.Equal(1, await db.Set<AgentInstallation>().CountAsync(a => a.InstallationId == enrollment.InstallationId, TestContext.Current.CancellationToken));
    }
    private sealed class AllowTestAdministration : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
