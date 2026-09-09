using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Tests;

public sealed class AgentDistributionTests
{
    [Fact]
    public async Task Bootstrap_uses_configured_https_origin_and_current_setup_hash_without_caching()
    {
        var root = Path.Combine(Path.GetTempPath(), "iam-agent-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var setupPath = Path.Combine(root, "IAM.Agent.Setup.zip");
            await File.WriteAllTextAsync(setupPath, "synthetic setup", TestContext.Current.CancellationToken);
            await using var factory = new IdentityApiFactory();
            await using var configured = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?> { ["AgentDistribution:RootPath"] = root })));
            using var client = configured.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/install.ps1");
            request.Headers.Host = "localhost:43210";
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var script = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("https://identity.test/api/v1/agents/download", script);
            Assert.DoesNotContain("localhost:43210", script);
            Assert.DoesNotContain("__SETUP_", script);
            Assert.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic setup"))), script);

            await File.WriteAllTextAsync(setupPath, "a new synthetic release", TestContext.Current.CancellationToken);
            var updated = await client.GetStringAsync("/install.ps1", TestContext.Current.CancellationToken);
            Assert.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("a new synthetic release"))), updated);

            await using var insecure = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AgentDistribution:RootPath"] = root,
                    ["Identity:Jwt:Issuer"] = "http://localhost",
                })));
            using var insecureClient = insecure.CreateClient();
            using var refused = await insecureClient.GetAsync("/install.ps1", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

            File.Delete(setupPath);
            using var missing = await client.GetAsync("/install.ps1", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(null, "https://identity.test")]
    [InlineData("relative/feed", "https://identity.test")]
    [InlineData(null, "http://localhost")]
    public async Task Bootstrap_requires_a_configured_feed_and_https_issuer(string? root, string issuer)
    {
        await using var factory = new IdentityApiFactory();
        await using var configured = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentDistribution:RootPath"] = root,
                ["Identity:Jwt:Issuer"] = issuer,
            })));
        using var client = configured.CreateClient();
        using var response = await client.GetAsync("/install.ps1", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_downloads_are_allowlisted_and_manifest_is_not_cached()
    {
        var root = Path.Combine(Path.GetTempPath(), "iam-agent-download-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "IAM.Agent.Setup.zip"), "synthetic setup", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "latest.json"), "{}", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "private.pem"), "synthetic forbidden file", TestContext.Current.CancellationToken);
            await using var factory = new IdentityApiFactory();
            await using var configured = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?> { ["AgentDistribution:RootPath"] = root })));
            using var client = configured.CreateClient();
            using var setup = await client.GetAsync("/api/v1/agents/download", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
            Assert.Equal("application/zip", setup.Content.Headers.ContentType?.MediaType);
            Assert.True(setup.Headers.CacheControl?.NoStore);
            Assert.Equal("synthetic setup", await setup.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var latest = await client.GetAsync("/api/v1/agents/releases/latest.json", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
            Assert.True(latest.Headers.CacheControl?.NoStore);
            using var forbidden = await client.GetAsync("/api/v1/agents/releases/private.pem", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        }
        finally { Directory.Delete(root, true); }
    }
}
