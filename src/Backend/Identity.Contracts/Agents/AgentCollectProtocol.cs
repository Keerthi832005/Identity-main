using System.Security.Cryptography;
using System.Text.Json;

namespace Identity.Contracts.Agents;

public sealed record AgentCollectAcknowledgement(Guid RequestId, string Result);
public sealed record AgentCollectReport(Guid InstallationId, DateTimeOffset SignedAt, string AgentVersion,
    AgentCollectAcknowledgement? Acknowledgement = null, string Purpose = "IAM.Agent.Collect.v1");
public sealed record AgentCollectCommand(Guid RequestId, DateTimeOffset ExpiresAt, string Kind = "collect-inventory");
public sealed record AgentCollectReply(AgentCollectCommand? Collect);

/// <summary>
/// The worker's own signed channel. Collection lives here rather than on the supervisor channel
/// because a worker updates itself over the signed feed, while a supervisor needs a local
/// administrator - so a supervisor-gated command would never reach an enrolled machine.
/// </summary>
public static class AgentCollectProtocol
{
    public static AgentCollectReport Verify(SignedMachineReport envelope, string publicKey, DateTimeOffset now)
    {
        if (envelope.Payload is null || envelope.Signature is null || envelope.Payload.Length > 12_000 || envelope.Signature.Length > 100)
            throw new ArgumentException("Invalid collect proof size.");
        var payload = Convert.FromBase64String(envelope.Payload);
        if (payload.Length > 8192) throw new ArgumentException("Collect proof too large.");
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(AgentProtocol.PublicKey(publicKey), out _);
        if (!key.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new CryptographicException("Invalid collect signature.");
        var report = JsonSerializer.Deserialize<AgentCollectReport>(payload, AgentProtocol.Json) ?? throw new ArgumentException("Missing collect report.");
        if (report.InstallationId == Guid.Empty || report.InstallationId != envelope.InstallationId
            || report.Purpose != "IAM.Agent.Collect.v1" || (now - report.SignedAt).Duration() > TimeSpan.FromMinutes(10))
            throw new ArgumentException("Invalid collect identity, purpose or timestamp.");
        AgentProtocol.Text(report.AgentVersion, 50);
        if (!Version.TryParse(report.AgentVersion, out var version) || version.Build < 0) throw new ArgumentException("Invalid agent version.");
        if (report.Acknowledgement is { } ack && (ack.RequestId == Guid.Empty
            || ack.Result is not ("collected" or "rejected" or "failed")))
            throw new ArgumentException("Unsupported collect acknowledgement.");
        return report;
    }
}
