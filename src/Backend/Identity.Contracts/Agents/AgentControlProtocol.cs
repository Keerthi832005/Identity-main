using System.Security.Cryptography;
using System.Text.Json;

namespace Identity.Contracts.Agents;

public sealed record AgentUpdateAcknowledgement(Guid RequestId, string Result);
public sealed record AgentControlReport(Guid InstallationId, DateTimeOffset SignedAt, string AgentVersion,
    string SupervisorVersion, AgentUpdateAcknowledgement? Acknowledgement = null, string Purpose = "IAM.Agent.Control.v1");
public sealed record AgentUpdateCommand(Guid RequestId, DateTimeOffset ExpiresAt, string Kind = "check-for-update");
public sealed record AgentControlReply(AgentUpdateCommand? Update);

public static class AgentControlProtocol
{
    public static AgentControlReport Verify(SignedMachineReport envelope, string publicKey, DateTimeOffset now)
    {
        if (envelope.Payload is null || envelope.Signature is null || envelope.Payload.Length > 12_000 || envelope.Signature.Length > 100)
            throw new ArgumentException("Invalid control proof size.");
        var payload = Convert.FromBase64String(envelope.Payload);
        if (payload.Length > 8192) throw new ArgumentException("Control proof too large.");
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(AgentProtocol.PublicKey(publicKey), out _);
        if (!key.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new CryptographicException("Invalid control signature.");
        var report = JsonSerializer.Deserialize<AgentControlReport>(payload, AgentProtocol.Json) ?? throw new ArgumentException("Missing control report.");
        if (report.InstallationId == Guid.Empty || report.InstallationId != envelope.InstallationId
            || report.Purpose != "IAM.Agent.Control.v1" || (now - report.SignedAt).Duration() > TimeSpan.FromMinutes(10))
            throw new ArgumentException("Invalid control identity, purpose or timestamp.");
        foreach (var value in new[] { report.AgentVersion, report.SupervisorVersion })
        {
            AgentProtocol.Text(value, 50);
            if (!Version.TryParse(value, out var version) || version.Build < 0) throw new ArgumentException("Invalid agent version.");
        }
        if (report.Acknowledgement is { } ack && (ack.RequestId == Guid.Empty || ack.Result is not ("updated" or "upToDate" or "failed")))
            throw new ArgumentException("Unsupported update acknowledgement.");
        return report;
    }
}
