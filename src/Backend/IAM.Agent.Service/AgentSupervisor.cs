using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;
using Identity.Contracts.Agents;

namespace IAM.Agent.Service;

public sealed record SupervisorPaths(string Root, string Data);
public sealed record AgentVersionState(string Current, string? Previous = null, string? Failed = null);
public sealed record AgentHealth(string Status, string Version, int ProcessId);

public sealed class AgentSupervisor(SupervisorPaths paths, ILogger<AgentSupervisor> log, HttpClient? updateClient = null) : BackgroundService
{
    private Process? child;
    private IDisposable? job;
    private string healthToken = "";
    private string updateOutcome = "upToDate";
    private readonly HttpClient control = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private string StatePath => Path.Combine(paths.Data, "version.json");
    private string ConfigPath => Path.Combine(paths.Data, "agent.json");
    private readonly HttpClient local = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
    private readonly HttpClient feed = updateClient ?? new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            AgentRelease.AssertNoLinks(paths.Root); AgentRelease.AssertNoLinks(paths.Data);
            var settings = AgentSettings.Load(ConfigPath);
            var state = JsonSerializer.Deserialize<AgentVersionState>(File.ReadAllText(StatePath), AgentSettings.Json) ?? throw new InvalidDataException("Version state missing.");
            AgentRelease.ParseVersion(state.Current);
            if (!await StartHealthy(state.Current, ct))
            {
                if (state.Previous is null || !await StartHealthy(state.Previous, ct)) throw new IOException("No healthy agent version available.");
                state = new(state.Previous, null, state.Current);
                AtomicJson.Write(StatePath, state);
            }
            var nextCheck = DateTimeOffset.UtcNow;
            var failures = 0;
            AgentUpdateAcknowledgement? acknowledgement = null;
            var supervisorVersion = typeof(AgentSupervisor).Assembly.GetName().Version!.ToString(3);
            while (!ct.IsCancellationRequested)
            {
                if (!await Healthy(state.Current, ct))
                {
                    failures++;
                    if (failures >= 3)
                    {
                        if (!await StartHealthy(state.Current, ct)) throw new IOException("Agent failed health recovery.");
                        failures = 0;
                    }
                }
                else failures = 0;
                if (DateTimeOffset.UtcNow >= nextCheck)
                {
                    nextCheck = DateTimeOffset.UtcNow.AddMinutes(settings.UpdateIntervalMinutes).AddSeconds(Random.Shared.Next(60));
                    try { state = await Update(settings, state, ct); }
                    catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
                    {
                        log.LogWarning("Agent update check failed ({ErrorType}); current version retained.", e.GetType().Name);
                        AtomicJson.Write(Path.Combine(paths.Data, "update-status.json"), new { checkedAt = DateTimeOffset.UtcNow, succeeded = false, error = e.GetType().Name });
                    }
                }
                try
                {
                    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                    using var key = DeviceKey.Read(paths.Data);
                    var reply = await new AgentControlClient(control).Poll(settings, state.Current, supervisorVersion, key, acknowledgement, ct);
                    acknowledgement = null;
                    AtomicJson.Write(Path.Combine(paths.Data, "control-status.json"), new { checkedAt = DateTimeOffset.UtcNow, succeeded = true, supervisorVersion });
                    if (reply.Update is { } command)
                    {
                        try { state = await Update(settings, state, ct); }
                        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
                        {
                            updateOutcome = "failed";
                            log.LogWarning("Requested agent update failed ({ErrorType}); current version retained.", e.GetType().Name);
                            AtomicJson.Write(Path.Combine(paths.Data, "update-status.json"), new { checkedAt = DateTimeOffset.UtcNow, succeeded = false, error = e.GetType().Name });
                        }
                        acknowledgement = new(command.RequestId, updateOutcome);
                        nextCheck = DateTimeOffset.UtcNow.AddMinutes(settings.UpdateIntervalMinutes);
                    }
                }
                catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
                {
                    // Control connectivity never interrupts the worker or the independent signed OTA schedule.
                    AtomicJson.Write(Path.Combine(paths.Data, "control-status.json"), new { checkedAt = DateTimeOffset.UtcNow, succeeded = false, error = e.GetType().Name, supervisorVersion });
                }
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception e)
        {
            log.LogCritical("IAM.Agent supervisor failed ({ErrorType}); Windows recovery will restart it.", e.GetType().Name);
            Environment.ExitCode = 1;
            StopChild();
            Environment.Exit(1);
        }
        finally { StopChild(); }
    }

    internal async Task<AgentVersionState> Update(AgentSettings settings, AgentVersionState state, CancellationToken ct)
    {
        updateOutcome = "failed";
        var manifestBytes = await Download(new Uri(new Uri(settings.UpdateFeed), "latest.json"), 16_384, ct);
        var release = AgentRelease.Verify(manifestBytes, settings.UpdatePublicKey, state.Current, DateTimeOffset.UtcNow);
        if (release.Version == state.Current) { updateOutcome = "upToDate"; return state; }
        if (release.Version == state.Failed) return state;
        var versions = Path.Combine(paths.Root, "versions");
        var final = Path.Combine(versions, release.Version);
        var staging = Path.Combine(versions, release.Version + ".staging-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(paths.Data, "download-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var response = await feed.GetAsync(new Uri(new Uri(settings.UpdateFeed), release.PackageFile), HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is { } size && size != release.Size) throw new InvalidDataException("Download size mismatch.");
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, ct)) != 0)
                {
                    total += read;
                    if (total > release.Size) throw new InvalidDataException("Download limit exceeded.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            await release.VerifyPackage(package, ct);
            AgentRelease.ExtractPackage(package, staging);
            // Never reuse an unverified directory left by an interrupted staging attempt.
            if (Directory.Exists(final)) Directory.Move(final, final + ".retained-" + Guid.NewGuid().ToString("N"));
            Directory.Move(staging, final);
            var previous = state.Current;
            var candidateHealthy = false;
            try { candidateHealthy = await StartHealthy(release.Version, ct); }
            catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
            { log.LogWarning("Candidate failed to launch ({ErrorType}).", e.GetType().Name); }
            if (candidateHealthy)
            {
                state = new(release.Version, previous);
                AtomicJson.Write(StatePath, state);
                updateOutcome = "updated";
                log.LogInformation("IAM.Agent updated to {Version}.", release.Version);
            }
            else
            {
                if (!await StartHealthy(previous, ct)) throw new IOException("Rollback agent failed health check.");
                state = state with { Failed = release.Version };
                AtomicJson.Write(StatePath, state);
                log.LogWarning("Rejected agent version {Version}; restored {Previous}.", release.Version, previous);
            }
            AtomicJson.Write(Path.Combine(paths.Data, "update-status.json"), new { checkedAt = DateTimeOffset.UtcNow, succeeded = state.Current == release.Version, current = state.Current, failed = state.Failed });
            return state;
        }
        finally { if (File.Exists(package)) File.Delete(package); }
    }

    private async Task<byte[]> Download(Uri uri, int maximum, CancellationToken ct)
    {
        using var response = await feed.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Manifest too large.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var destination = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            if (destination.Length + read > maximum) throw new InvalidDataException("Manifest too large.");
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }

    internal async Task<bool> StartHealthy(string version, CancellationToken ct)
    {
        StopChild();
        AgentRelease.ParseVersion(version);
        var executable = Path.Combine(paths.Root, "versions", version, "IAM.Agent.exe");
        AgentRelease.AssertNoLinks(Path.GetDirectoryName(executable)!);
        if (!File.Exists(executable)) return false;
        healthToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add("--config"); start.ArgumentList.Add(ConfigPath);
        start.Environment["IAM_AGENT_HEALTH_TOKEN"] = healthToken;
        start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = Path.Combine(paths.Data, "runtime");
        try
        {
            child = Process.Start(start);
            if (child is not null && OperatingSystem.IsWindows()) job = new ProcessJob(child);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            StopChild();
            return false;
        }
        for (var i = 0; i < 20 && child is { HasExited: false }; i++)
        {
            if (await Healthy(version, ct)) return true;
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        StopChild();
        return false;
    }

    private async Task<bool> Healthy(string version, CancellationToken ct)
    {
        if (child is null || child.HasExited) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{AgentSettings.Port}/health");
            request.Headers.Add("X-IAM-Health", healthToken);
            using var response = await local.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            var health = await response.Content.ReadFromJsonAsync<AgentHealth>(ct);
            return health is { Status: "ready" } && health.Version == version && health.ProcessId == child.Id;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { return false; }
    }

    private void StopChild()
    {
        if (child is null) return;
        try { if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(10_000); } }
        finally { job?.Dispose(); job = null; child.Dispose(); child = null; }
    }
    public override void Dispose() { StopChild(); local.Dispose(); feed.Dispose(); control.Dispose(); base.Dispose(); }
}
