using System.Security.Cryptography;
using System.Text.Json;
using Identity.Contracts.Agents;

namespace IAM.Agent.Tests;

public sealed class WindowsInventoryTests(ITestOutputHelper output)
{
    [Fact]
    public void Opt_in_local_windows_collection_produces_valid_user_metadata()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("IAM_TEST_COLLECT_CURRENT_PC") != "1")
        {
            Assert.Skip("Set IAM_TEST_COLLECT_CURRENT_PC=1 to read this Windows computer's inventory without posting it.");
            return;
        }
        var report = MachineCollector.Collect(Guid.NewGuid()) with { Software = [] };
        var users = Assert.IsType<WindowsUserInventory>(report.WindowsUsers);
        Assert.InRange(users.Sessions.Length, 0, 128);
        Assert.InRange(users.Profiles.Length, 0, 256);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
        var envelope = new SignedMachineReport(report.InstallationId, Convert.ToBase64String(payload),
            Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        AgentProtocol.Verify(envelope, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow);
        output.WriteLine($"Sessions: {users.Sessions.Length}; profiles: {users.Profiles.Length}; current user available: {users.CurrentUserName is not null}; sessions complete: {users.SessionsReported}; profiles complete: {users.ProfilesReported}.");
    }
}
