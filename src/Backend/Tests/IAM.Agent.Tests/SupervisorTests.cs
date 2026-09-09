using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;
using IAM.Agent.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace IAM.Agent.Tests;

[Collection("Agent process tests")]
public sealed class SupervisorTests
{
    [Fact]
    public async Task Real_agent_updates_then_rolls_back_invalid_signed_candidate_and_retains_state()
    {
        var executable = Environment.GetEnvironmentVariable("IAM_AGENT_TEST_EXECUTABLE");
        var upgrade = Environment.GetEnvironmentVariable("IAM_AGENT_TEST_UPGRADE_EXECUTABLE");
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(upgrade))
            Assert.Skip("Set IAM_AGENT_TEST_EXECUTABLE and IAM_AGENT_TEST_UPGRADE_EXECUTABLE to published Windows agents 1.0.0 and 1.0.1.");
        var root = Path.Combine(Path.GetTempPath(), "iam-agent-process-test-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(Path.Combine(root, "versions", "1.0.0"));
        File.Copy(executable!, Path.Combine(root, "versions", "1.0.0", "IAM.Agent.exe"));
        using var signing = RSA.Create(3072);
        var settings = new AgentSettings("https://pts.example", Guid.NewGuid(), 123, "https://agent-acceptance.invalid/feed/", signing.ExportSubjectPublicKeyInfoPem(), IdentityBaseUrl: "https://agent-acceptance.invalid/");
        AtomicJson.Write(Path.Combine(data, "agent.json"), settings);
        AtomicJson.Write(Path.Combine(data, "version.json"), new AgentVersionState("1.0.0"));
        byte[] package;
        using (var stream = new MemoryStream())
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            { using var writer = new StreamWriter(zip.CreateEntry("IAM.Agent.exe").Open()); writer.Write("Not a Windows executable"); }
            package = stream.ToArray();
        }
        byte[] goodPackage;
        using (var stream = new MemoryStream())
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) zip.CreateEntryFromFile(upgrade!, "IAM.Agent.exe");
            goodPackage = stream.ToArray();
        }
        byte[] Manifest(string version, byte[] content)
        {
            var release = new AgentRelease("IAM.Agent", 1, version, "win-x64", $"IAM.Agent-{version}-win-x64.zip", content.Length, Convert.ToHexString(SHA256.HashData(content)), DateTimeOffset.UtcNow.AddDays(1), "1.0.0");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(release, AgentSettings.Json);
            return JsonSerializer.SerializeToUtf8Bytes(new ReleaseEnvelope(Convert.ToBase64String(bytes), Convert.ToBase64String(signing.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), AgentSettings.Json);
        }
        var handler = new FeedHandler { Manifest = Manifest("1.0.1", goodPackage), Package = goodPackage };
        using var client = new HttpClient(handler);
        try
        {
            using var supervisor = new AgentSupervisor(new SupervisorPaths(root, data), NullLogger<AgentSupervisor>.Instance, client);
            Assert.True(await supervisor.StartHealthy("1.0.0", TestContext.Current.CancellationToken));
            var state = await supervisor.Update(settings, new("1.0.0"), TestContext.Current.CancellationToken);
            Assert.Equal("1.0.1", state.Current);
            Assert.Equal("1.0.0", state.Previous);
            Assert.Null(state.Failed);
            handler.Manifest = Manifest("1.0.2", package); handler.Package = package;
            state = await supervisor.Update(settings, state, TestContext.Current.CancellationToken);
            Assert.Equal("1.0.1", state.Current);
            Assert.Equal("1.0.2", state.Failed);
            var stored = JsonSerializer.Deserialize<AgentVersionState>(File.ReadAllText(Path.Combine(data, "version.json")), AgentSettings.Json)!;
            Assert.Equal(state, stored);
            Assert.Equal(state, await supervisor.Update(settings, state, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class FeedHandler : HttpMessageHandler
    {
        public required byte[] Manifest { get; set; }
        public required byte[] Package { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(request.RequestUri!.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal) ? Manifest : Package) });
    }
}
