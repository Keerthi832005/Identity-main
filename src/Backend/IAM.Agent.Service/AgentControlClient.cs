using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;
using Identity.Contracts.Agents;

namespace IAM.Agent.Service;

public sealed class AgentControlClient(HttpClient client)
{
    public async Task<AgentControlReply> Poll(AgentSettings settings, string currentVersion, string supervisorVersion,
        ECDsa key, AgentUpdateAcknowledgement? acknowledgement, CancellationToken ct)
    {
        var report = new AgentControlReport(settings.InstallationId, DateTimeOffset.UtcNow, currentVersion, supervisorVersion, acknowledgement);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
        var proof = new SignedMachineReport(settings.InstallationId, Convert.ToBase64String(bytes),
            Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.IdentityBaseUrl), "api/v1/agents/control"))
        { Content = JsonContent.Create(proof, options: AgentProtocol.Json) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 16_384) throw new InvalidDataException("Control reply too large.");
        await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var output = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await input.ReadAsync(buffer, deadline.Token)) != 0)
        {
            if (output.Length + read > 16_384) throw new InvalidDataException("Control reply too large.");
            output.Write(buffer, 0, read);
        }
        var reply = JsonSerializer.Deserialize<AgentControlReply>(output.ToArray(), AgentProtocol.Json)
            ?? throw new InvalidDataException("Control reply missing.");
        if (reply.Update is { } command && (command.Kind != "check-for-update" || command.RequestId == Guid.Empty
            || command.ExpiresAt <= DateTimeOffset.UtcNow || command.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(25)))
            throw new InvalidDataException("Unsupported or expired update request.");
        return reply;
    }
}
