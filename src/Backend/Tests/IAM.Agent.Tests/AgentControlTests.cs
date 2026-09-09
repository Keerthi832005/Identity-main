using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;
using IAM.Agent.Service;
using Identity.Contracts.Agents;

namespace IAM.Agent.Tests;

public sealed class AgentControlTests
{
    [Fact]
    public void Control_proof_requires_registered_key_identity_purpose_timestamp_and_supported_acknowledgement()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var now = DateTimeOffset.UtcNow;
        var report = new AgentControlReport(Guid.NewGuid(), now, "1.0.1", "1.0.1");
        SignedMachineReport Sign(AgentControlReport value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AgentProtocol.Json);
            return new(value.InstallationId, Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        var proof = Sign(report);
        Assert.Equal(report, AgentControlProtocol.Verify(proof, publicKey, now));
        Assert.Throws<CryptographicException>(() => AgentControlProtocol.Verify(proof, Convert.ToBase64String(wrongKey.ExportSubjectPublicKeyInfo()), now));
        Assert.Throws<ArgumentException>(() => AgentControlProtocol.Verify(proof with { InstallationId = Guid.NewGuid() }, publicKey, now));
        foreach (var invalid in new[] { report with { SignedAt = now.AddMinutes(-11) }, report with { SignedAt = now.AddMinutes(11) },
            report with { Purpose = "inventory" }, report with { AgentVersion = "bad" },
            report with { Acknowledgement = new(Guid.NewGuid(), "run-shell") } })
            Assert.Throws<ArgumentException>(() => AgentControlProtocol.Verify(Sign(invalid), publicKey, now));
        Assert.Throws<ArgumentException>(() => AgentControlProtocol.Verify(proof with { Payload = new string('a', 12001) }, publicKey, now));
    }

    [Fact]
    public void Collect_proof_accepts_only_the_three_outcomes_the_worker_can_report()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var now = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var report = new AgentCollectReport(Guid.NewGuid(), now, "1.0.6");
        SignedMachineReport Sign(AgentCollectReport value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AgentProtocol.Json);
            return new(value.InstallationId, Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        foreach (var result in new[] { "collected", "rejected", "failed" })
        {
            var acknowledged = report with { Acknowledgement = new(requestId, result) };
            Assert.Equal(acknowledged, AgentCollectProtocol.Verify(Sign(acknowledged), publicKey, now));
        }
        foreach (var invalid in new[] { report with { Acknowledgement = new(requestId, "done") },
            report with { Acknowledgement = new(Guid.Empty, "collected") } })
            Assert.Throws<ArgumentException>(() => AgentCollectProtocol.Verify(Sign(invalid), publicKey, now));
    }

    [Fact]
    public async Task Client_signs_outbound_heartbeat_and_accepts_only_bounded_known_unexpired_update_commands()
    {
        var ct = TestContext.Current.CancellationToken;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = new AgentSettings("https://pts.test", Guid.NewGuid(), 1, "https://iam.test/feed/", "unused", IdentityBaseUrl: "https://iam.test/identity/");
        var command = new AgentUpdateCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(15));
        var acknowledgement = new AgentUpdateAcknowledgement(Guid.NewGuid(), "updated");
        var responseBody = JsonSerializer.Serialize(new AgentControlReply(command), AgentProtocol.Json);
        var handler = new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://iam.test/identity/api/v1/agents/control", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            var proof = await request.Content!.ReadFromJsonAsync<SignedMachineReport>(ct);
            var report = AgentControlProtocol.Verify(proof!, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow);
            Assert.Equal(acknowledgement, report.Acknowledgement);
            Assert.Equal("1.0.1", report.SupervisorVersion);
            return new(HttpStatusCode.OK) { Content = new StringContent(responseBody) };
        });
        using var http = new HttpClient(handler);
        var client = new AgentControlClient(http);
        Assert.Equal(command, (await client.Poll(settings, "1.0.1", "1.0.1", key, acknowledgement, ct)).Update);
        foreach (var invalid in new[] { command with { Kind = "run-shell" }, command with { RequestId = Guid.Empty },
            command with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }, command with { ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) } })
        {
            responseBody = JsonSerializer.Serialize(new AgentControlReply(invalid), AgentProtocol.Json);
            await Assert.ThrowsAsync<InvalidDataException>(() => client.Poll(settings, "1.0.1", "1.0.1", key, acknowledgement, ct));
        }
        responseBody = new string(' ', 16385);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.Poll(settings, "1.0.1", "1.0.1", key, acknowledgement, ct));
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request); }
}
