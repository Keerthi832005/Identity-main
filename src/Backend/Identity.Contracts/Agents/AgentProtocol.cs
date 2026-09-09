using System.Security.Cryptography;
using System.Text.Json;

namespace Identity.Contracts.Agents;

public sealed record AgentEnrollment(Guid InstallationId, string Hostname, string PublicKey);
public sealed record AgentEnrollmentResult(Guid InstallationId, long TerminalId, bool Trusted);
public sealed record SignedMachineReport(Guid InstallationId, string Payload, string Signature);
public sealed record AgentAttestationChallenge(string Nonce, DateTimeOffset ExpiresAt);
public sealed record AgentAttestationRequest(Guid InstallationId, string Nonce, string Signature);
public sealed record InstalledSoftware(
    string Name,
    string Version,
    string Publisher,
    DateOnly? InstalledOn = null,
    long? EstimatedSizeBytes = null,
    string? RegistryView = null);
public sealed record DiskInventory(string Name, long TotalBytes, long FreeBytes);
public sealed record HardwareInventory(string Manufacturer, string Model, string SerialNumber, string Cpu,
    int LogicalProcessors, long MemoryBytes, DiskInventory[] Disks);
public sealed record MachineDiagnostics(long SystemUptimeSeconds, long AgentUptimeSeconds);
public sealed record WindowsSession(int SessionId, string UserName, string State, bool IsConsole);
public sealed record WindowsProfile(string Sid, string UserName, bool Loaded);
public sealed record WindowsUserInventory(string? CurrentUserName, WindowsSession[] Sessions, WindowsProfile[] Profiles,
    bool SessionsReported = true, bool ProfilesReported = true);
public sealed record MachineInventory(Guid InstallationId, DateTimeOffset CapturedAt, string Hostname,
    string AgentVersion, string Os, string Architecture, HardwareInventory Hardware,
    InstalledSoftware[] Software, string[] CollectionWarnings, MachineDiagnostics? Diagnostics = null,
    WindowsUserInventory? WindowsUsers = null);

public static class AgentProtocol
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const int MaximumPayloadBytes = 350_000;

    // Both sides derive the signed bytes here; drift would fail verification, not weaken it.
    public static byte[] AttestationPayload(Guid installationId, string nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce) || nonce.Length is < 16 or > 128)
        {
            throw new ArgumentException("Invalid attestation nonce.", nameof(nonce));
        }

        return System.Text.Encoding.UTF8.GetBytes($"iam-terminal-attestation:v1:{installationId:D}:{nonce}");
    }

    public static bool VerifyAttestation(AgentAttestationRequest request, string publicKey)
    {
        if (request.Signature is null || request.Signature.Length > 200) return false;
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(PublicKey(publicKey), out _);
        return key.VerifyData(
            AttestationPayload(request.InstallationId, request.Nonce),
            Convert.FromBase64String(request.Signature),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public static byte[] PublicKey(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 256) throw new ArgumentException("Invalid agent public key.");
        var bytes = Convert.FromBase64String(encoded);
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(bytes, out var read);
        if (read != bytes.Length || key.KeySize != 256
            || key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            throw new ArgumentException("Agent keys must be ECDSA P-256.");
        return bytes;
    }

    public static MachineInventory Verify(SignedMachineReport envelope, string publicKey, DateTimeOffset now)
    {
        if (envelope.Payload is null || envelope.Signature is null || envelope.Payload.Length > 470_000 || envelope.Signature.Length > 100)
            throw new ArgumentException("Invalid report size.");
        var payload = Convert.FromBase64String(envelope.Payload);
        if (payload.Length > MaximumPayloadBytes) throw new ArgumentException("Report is too large.");
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(PublicKey(publicKey), out _);
        if (!key.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new CryptographicException("Invalid report signature.");
        var report = JsonSerializer.Deserialize<MachineInventory>(payload, Json) ?? throw new ArgumentException("Missing report.");
        if (report.InstallationId != envelope.InstallationId || report.InstallationId == Guid.Empty
            || (now - report.CapturedAt).Duration() > TimeSpan.FromMinutes(10))
            throw new ArgumentException("Report identity or timestamp is invalid.");
        Text(report.Hostname, 200); Text(report.AgentVersion, 50); Text(report.Os, 256); Text(report.Architecture, 30);
        if (report.Hardware is null || report.Software is null || report.CollectionWarnings is null
            || report.Software.Length > 2000 || report.CollectionWarnings.Length > 20)
            throw new ArgumentException("Invalid inventory.");
        var hardware = report.Hardware;
        Text(hardware.Manufacturer, 200, true); Text(hardware.Model, 200, true); Text(hardware.SerialNumber, 200, true); Text(hardware.Cpu, 200, true);
        if (hardware.MemoryBytes < 0 || hardware.LogicalProcessors is < 0 or > 4096 || hardware.Disks is null || hardware.Disks.Length > 64)
            throw new ArgumentException("Invalid hardware values.");
        foreach (var disk in hardware.Disks)
        {
            if (disk is null || disk.TotalBytes < 0 || disk.FreeBytes < 0 || disk.FreeBytes > disk.TotalBytes)
                throw new ArgumentException("Invalid disk values.");
            Text(disk.Name, 100);
        }
        foreach (var software in report.Software)
        {
            if (software is null) throw new ArgumentException("Invalid software entry.");
            Text(software.Name, 200); Text(software.Version, 100, true); Text(software.Publisher, 200, true);
            if (software.EstimatedSizeBytes is < 0 or > 4_398_046_510_080
                || software.RegistryView is not (null or "32-bit" or "64-bit"))
                throw new ArgumentException("Invalid software metadata.");
        }
        foreach (var warning in report.CollectionWarnings) Text(warning, 200);
        if (report.Diagnostics is { } diagnostics && (diagnostics.SystemUptimeSeconds is < 0 or > 3_155_760_000
            || diagnostics.AgentUptimeSeconds < 0 || diagnostics.AgentUptimeSeconds > diagnostics.SystemUptimeSeconds))
            throw new ArgumentException("Invalid diagnostic values.");
        if (report.WindowsUsers is { } users)
        {
            if (users.CurrentUserName is not null) Text(users.CurrentUserName, 256);
            if (users.Sessions is null || users.Profiles is null || users.Sessions.Length > 128 || users.Profiles.Length > 256)
                throw new ArgumentException("Invalid Windows user inventory.");
            foreach (var session in users.Sessions)
            {
                if (session is null || session.SessionId < 0 || session.State is not ("Active" or "Connected" or "Disconnected" or "Other"))
                    throw new ArgumentException("Invalid Windows session.");
                Text(session.UserName, 256);
            }
            foreach (var profile in users.Profiles)
            {
                if (profile is null) throw new ArgumentException("Invalid Windows profile.");
                Text(profile.Sid, 184); Text(profile.UserName, 256);
            }
        }
        return report;
    }

    public static void Text(string? value, int max, bool allowEmpty = false)
    {
        if (value is null || value.Length > max || (!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.Any(char.IsControl))
            throw new ArgumentException("Invalid inventory text.");
    }
}
