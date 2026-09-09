using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;
using Identity.Contracts.Agents;

namespace IAM.Agent;

public sealed class MachineReporter(AgentSettings settings, string directory) : BackgroundService
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    // One report at a time: a requested collection must not race the scheduled one over the same key and status file.
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Collects and reports now, for an administrator who will not wait for the next cycle.</summary>
    public async Task<string> ReportNow(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return "failed";
        await gate.WaitAsync(ct);
        try { return await Report(ct); }
        finally { gate.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows()) return;
        var due = DateTimeOffset.MinValue;
        var handled = new HashSet<Guid>();
        AgentCollectAcknowledgement? acknowledgement = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            AgentCollectCommand? command = null;
            try { command = (await PollCollect(acknowledgement, stoppingToken))?.Collect; acknowledgement = null; }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested && e is not OutOfMemoryException)
            {
                AtomicJson.Write(Path.Combine(directory, "collect-status.json"),
                    new { checkedAt = DateTimeOffset.UtcNow, succeeded = false, error = e.GetType().Name });
            }
            // A redelivered command is the same request: collect once, then keep answering with its result.
            var requested = command is not null && handled.Add(command.RequestId);
            if (requested || DateTimeOffset.UtcNow >= due)
            {
                await gate.WaitAsync(stoppingToken);
                string outcome;
                try { outcome = await Report(stoppingToken); }
                finally { gate.Release(); }
                due = DateTimeOffset.UtcNow.AddMinutes(30);
                if (requested) acknowledgement = new(command!.RequestId, outcome);
            }
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    /// <summary>The worker owns this channel: it updates itself over the signed feed, the supervisor does not.</summary>
    [SupportedOSPlatform("windows")]
    private async Task<AgentCollectReply?> PollCollect(AgentCollectAcknowledgement? acknowledgement, CancellationToken ct)
    {
        var report = new AgentCollectReport(settings.InstallationId, DateTimeOffset.UtcNow, AgentWeb.Version, acknowledgement);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
        using var key = DeviceKey.Read(directory);
        var proof = new SignedMachineReport(settings.InstallationId, Convert.ToBase64String(bytes),
            Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        using var response = await client.PostAsJsonAsync(new Uri(new Uri(settings.IdentityBaseUrl), "api/v1/agents/collect"), proof, ct);
        AtomicJson.Write(Path.Combine(directory, "collect-status.json"),
            new { checkedAt = DateTimeOffset.UtcNow, succeeded = response.IsSuccessStatusCode, httpStatus = (int)response.StatusCode });
        if (!response.IsSuccessStatusCode) return null;
        var reply = await response.Content.ReadFromJsonAsync<AgentCollectReply>(AgentProtocol.Json, ct);
        if (reply?.Collect is { } collect && (collect.Kind != "collect-inventory" || collect.RequestId == Guid.Empty
            || collect.ExpiresAt <= DateTimeOffset.UtcNow || collect.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(15)))
            throw new InvalidDataException("Unsupported or expired collection request.");
        return reply;
    }

    /// <summary>"collected", "rejected" (gathered but the server would not take it) or "failed" (could not gather).</summary>
    [SupportedOSPlatform("windows")]
    private async Task<string> Report(CancellationToken ct)
    {
        SignedMachineReport signed;
        // Gathering and delivery fail for unrelated reasons, so they are reported apart: an
        // administrator sent to the machine's WMI for what was a rejected POST looks in the wrong place.
        try
        {
            var report = MachineCollector.Collect(settings.InstallationId);
            var payload = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
            while (payload.Length > AgentProtocol.MaximumPayloadBytes && report.Software.Length > 0)
            {
                report = report with
                {
                    Software = report.Software.Take(Math.Max(0, report.Software.Length - 100)).ToArray(),
                    CollectionWarnings = report.CollectionWarnings.Append("Software list truncated to fit report size limit.").Distinct().ToArray()
                };
                payload = JsonSerializer.SerializeToUtf8Bytes(report, AgentProtocol.Json);
            }
            using var key = DeviceKey.Read(directory);
            signed = new SignedMachineReport(settings.InstallationId, Convert.ToBase64String(payload),
                Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
        {
            AtomicJson.Write(Path.Combine(directory, "report-status.json"),
                new { attemptedAt = DateTimeOffset.UtcNow, succeeded = false, stage = "collect", error = e.GetType().Name });
            return "failed";
        }
        try
        {
            using var response = await client.PostAsJsonAsync(new Uri(new Uri(settings.IdentityBaseUrl), "api/v1/agents/report"), signed, ct);
            AtomicJson.Write(Path.Combine(directory, "report-status.json"),
                new { attemptedAt = DateTimeOffset.UtcNow, succeeded = response.IsSuccessStatusCode, stage = "send", httpStatus = (int)response.StatusCode });
            return response.IsSuccessStatusCode ? "collected" : "rejected";
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
        {
            AtomicJson.Write(Path.Combine(directory, "report-status.json"),
                new { attemptedAt = DateTimeOffset.UtcNow, succeeded = false, stage = "send", error = e.GetType().Name });
            return "rejected";
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        client.Dispose();
        gate.Dispose();
    }
}
