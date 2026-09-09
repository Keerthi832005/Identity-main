using System.Security.Cryptography;
using System.Text.Json;
using Identity.Contracts.Agents;

namespace IAM.Agent.Tests;

public sealed class ReportProofTests
{
    [Fact]
    public void Reports_require_registered_key_matching_identity_fresh_timestamp_and_bounded_inventory()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var report = new MachineInventory(id, now, "HOST", "1.0.0", "Windows", "X64",
            new("Vendor", "Model", "Serial", "CPU", 8, 16_000_000_000, [new("C:", 100, 50)]),
            [new("Application", "1.0", "Publisher")], []);
        SignedMachineReport Sign(MachineInventory value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AgentProtocol.Json);
            return new(id, Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        var signed = Sign(report);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        Assert.Equal("HOST", AgentProtocol.Verify(signed, publicKey, now).Hostname);
        Assert.Null(AgentProtocol.Verify(signed, publicKey, now).Diagnostics);
        Assert.Null(AgentProtocol.Verify(signed, publicKey, now).WindowsUsers);
        var users = new WindowsUserInventory("DOMAIN\\worker", [new(1, "DOMAIN\\worker", "Active", true)],
            [new("S-1-5-21-1", "DOMAIN\\worker", true)]);
        var withUsers = AgentProtocol.Verify(Sign(report with { WindowsUsers = users }), publicKey, now).WindowsUsers!;
        Assert.Equal(users.CurrentUserName, withUsers.CurrentUserName);
        Assert.Equal(users.Sessions, withUsers.Sessions);
        Assert.Equal(users.Profiles, withUsers.Profiles);
        foreach (var invalid in new[] {
            users with { CurrentUserName = "bad\nuser" },
            users with { Sessions = new WindowsSession[129] },
            users with { Profiles = new WindowsProfile[257] },
            users with { Sessions = [new(-1, "user", "Active", false)] },
            users with { Sessions = [new(1, "user", "made-up", false)] },
        }) Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(Sign(report with { WindowsUsers = invalid }), publicKey, now));
        var legacy = AgentProtocol.Verify(signed, publicKey, now).Software[0];
        Assert.Null(legacy.InstalledOn);
        Assert.Null(legacy.EstimatedSizeBytes);
        Assert.Null(legacy.RegistryView);
        var oldJson = JsonSerializer.Deserialize<InstalledSoftware>("""{"name":"Legacy","version":"1","publisher":"Vendor"}""", AgentProtocol.Json);
        Assert.Equal(new InstalledSoftware("Legacy", "1", "Vendor"), oldJson);
        var software = new InstalledSoftware("Application", "1.0", "Publisher", new DateOnly(2026, 8, 31), 1048576, "64-bit");
        Assert.Equal(software, AgentProtocol.Verify(Sign(report with { Software = [software] }), publicKey, now).Software[0]);
        foreach (var invalid in new[] { software with { EstimatedSizeBytes = -1 }, software with { EstimatedSizeBytes = long.MaxValue }, software with { RegistryView = "unknown" } })
            Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(Sign(report with { Software = [invalid] }), publicKey, now));
        var diagnostics = new MachineDiagnostics(3600, 1800);
        Assert.Equal(diagnostics, AgentProtocol.Verify(Sign(report with { Diagnostics = diagnostics }), publicKey, now).Diagnostics);
        foreach (var invalid in new[] { new MachineDiagnostics(-1, 0), new MachineDiagnostics(10, 11), new MachineDiagnostics(10, -1), new MachineDiagnostics(long.MaxValue, 1) })
            Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(Sign(report with { Diagnostics = invalid }), publicKey, now));
        Assert.Throws<CryptographicException>(() => AgentProtocol.Verify(signed, Convert.ToBase64String(wrong.ExportSubjectPublicKeyInfo()), now));
        Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(signed with { InstallationId = Guid.NewGuid() }, publicKey, now));
        Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(signed, publicKey, now.AddMinutes(11)));
        Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(Sign(report with { Hostname = new string('x', 201) }), publicKey, now));
        Assert.Throws<ArgumentException>(() => AgentProtocol.Verify(Sign(report with { Software = new InstalledSoftware[2001] }), publicKey, now));
    }

    [Fact]
    public void Attestation_accepts_only_the_enrolled_key_and_the_nonce_that_was_signed()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var installationId = Guid.NewGuid();
        var nonce = new string('n', 32);
        var request = Sign(key, installationId, nonce);

        Assert.True(AgentProtocol.VerifyAttestation(request, publicKey));
        Assert.False(AgentProtocol.VerifyAttestation(
            Sign(other, installationId, nonce), publicKey));
        Assert.False(AgentProtocol.VerifyAttestation(
            request with { Nonce = new string('m', 32) }, publicKey));
        Assert.False(AgentProtocol.VerifyAttestation(
            request with { InstallationId = Guid.NewGuid() }, publicKey));
    }

    [Fact]
    public void Attestation_payload_binds_the_installation_and_bounds_the_nonce()
    {
        var installationId = Guid.NewGuid();
        var nonce = new string('n', 32);

        Assert.Equal(
            AgentProtocol.AttestationPayload(installationId, nonce),
            AgentProtocol.AttestationPayload(installationId, nonce));
        Assert.NotEqual(
            AgentProtocol.AttestationPayload(installationId, nonce),
            AgentProtocol.AttestationPayload(Guid.NewGuid(), nonce));
        Assert.Throws<ArgumentException>(() => AgentProtocol.AttestationPayload(installationId, "short"));
        Assert.Throws<ArgumentException>(() => AgentProtocol.AttestationPayload(installationId, new string('n', 129)));
    }

    private static AgentAttestationRequest Sign(ECDsa key, Guid installationId, string nonce) => new(
        installationId,
        nonce,
        Convert.ToBase64String(key.SignData(
            AgentProtocol.AttestationPayload(installationId, nonce),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
}
