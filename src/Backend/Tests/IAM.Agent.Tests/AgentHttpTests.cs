using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using IAM.Agent.Core;

namespace IAM.Agent.Tests;

public sealed class AgentHttpTests
{
    internal static AgentSettings Settings()
    {
        using var key = RSA.Create(3072);
        return new("https://pts.example", Guid.NewGuid(), 123, "https://pts.example/agent/", key.ExportSubjectPublicKeyInfoPem(), IdentityBaseUrl: "https://pts.example/identity/");
    }

    [Fact]
    public async Task Browser_reads_identity_but_untrusted_origins_hosts_and_writes_are_denied()
    {
        var settings = Settings();
        await using var app = AgentWeb.Build(settings, new string('a', 64), 0);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/identity");
        request.Headers.Add("Origin", settings.SiteOrigin);
        request.Headers.Add("X-IAM-Agent", "1");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(settings.SiteOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var identity = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(123, identity.GetProperty("terminalId").GetInt64());
        Assert.Equal(Environment.MachineName, identity.GetProperty("hostname").GetString());
        Assert.False(identity.TryGetProperty("installationId", out _));
        Assert.True(response.Headers.CacheControl!.NoStore);
        foreach (var origin in new[] { "https://attacker.example", "null", settings.SiteOrigin + ".attacker.example", "" })
        {
            using var bad = new HttpRequestMessage(HttpMethod.Get, "/v1/identity");
            bad.Headers.TryAddWithoutValidation("Origin", origin);
            bad.Headers.Add("X-IAM-Agent", "1");
            using var denied = await client.SendAsync(bad, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var rebind = new HttpRequestMessage(HttpMethod.Get, "/v1/identity");
        rebind.Headers.Host = "attacker.example";
        rebind.Headers.Add("Origin", settings.SiteOrigin);
        rebind.Headers.Add("X-IAM-Agent", "1");
        using var rebound = await client.SendAsync(rebind, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, rebound.StatusCode);
        using var write = new HttpRequestMessage(HttpMethod.Post, "/v1/identity");
        write.Headers.Add("Origin", settings.SiteOrigin);
        write.Headers.Add("X-IAM-Agent", "1");
        using var written = await client.SendAsync(write, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, written.StatusCode);
        using var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, health.StatusCode);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/v1/identity");
        preflight.Headers.Add("Origin", settings.SiteOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-iam-agent");
        using var preflightResult = await client.SendAsync(preflight, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, preflightResult.StatusCode);
        Assert.Equal("GET", preflightResult.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Equal("X-IAM-Agent", preflightResult.Headers.GetValues("Access-Control-Allow-Headers").Single());
        using var foreignHeader = new HttpRequestMessage(HttpMethod.Options, "/v1/identity");
        foreignHeader.Headers.Add("Origin", settings.SiteOrigin);
        foreignHeader.Headers.Add("Access-Control-Request-Method", "GET");
        foreignHeader.Headers.Add("Access-Control-Request-Headers", "x-iam-agent,authorization");
        using var headerDenied = await client.SendAsync(foreignHeader, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, headerDenied.StatusCode);
    }

    [Fact]
    public void Configuration_rejects_foreign_update_hosts_invalid_identity_and_weak_keys()
    {
        var valid = Settings();
        valid.Validate();
        Assert.Throws<InvalidDataException>(() => (valid with { TerminalId = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { SiteOrigin = "http://pts.example" }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { UpdateFeed = "https://attacker.example/" }).Validate());
        using var weak = RSA.Create(2048);
        Assert.Throws<InvalidDataException>(() => (valid with { UpdatePublicKey = weak.ExportSubjectPublicKeyInfoPem() }).Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Loopback_development_origins_require_opt_in_and_preserve_request_guards(bool enabled)
    {
        var settings = Settings() with { AllowLoopbackDevelopmentOrigins = enabled };
        await using var app = AgentWeb.Build(settings, new string('b', 64), 0);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
        var localOrigins = new[] { "http://localhost", "http://localhost:80", "http://localhost:4301", "http://localhost:65535", "http://127.0.0.1", "http://127.0.0.1:4303", "http://127.0.0.1:1" };
        var foreignOrigins = new[] { "http://localhost.attacker.example", "http://127.0.0.1.attacker.example", "http://localhost@attacker.example", "http://attacker@localhost", "http://127.0.0.10:4303", "http://127.1:4303", "http://2130706433", "http://localhost/", "http://localhost?x=1", "http://localhost#x", "http://localhost:0", "http://localhost:65536", "http://localhost:4303 https://pts.example", "http://[::1]:4303", "http://192.168.1.2:4303", "null" };
        foreach (var origin in localOrigins.Concat(foreignOrigins).Append(settings.SiteOrigin))
        {
            var allowed = origin == settings.SiteOrigin || enabled && localOrigins.Contains(origin);
            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Options })
            {
                using var request = new HttpRequestMessage(method, "/v1/identity");
                request.Headers.TryAddWithoutValidation("Origin", origin);
                if (method == HttpMethod.Get) request.Headers.Add("X-IAM-Agent", "1");
                else
                {
                    request.Headers.Add("Access-Control-Request-Method", "GET");
                    request.Headers.Add("Access-Control-Request-Headers", "x-iam-agent");
                }
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                Assert.Equal(allowed ? method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
                if (!allowed) { Assert.False(response.Headers.Contains("Access-Control-Allow-Origin")); continue; }
                Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
                if (method == HttpMethod.Get)
                {
                    var identity = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
                    Assert.Equal(origin, identity.GetProperty("siteOrigin").GetString());
                    Assert.Equal(settings.TerminalId, identity.GetProperty("terminalId").GetInt64());
                }
            }
        }
        foreach (var guard in new[] { "missing-header", "write", "foreign-host", "authorization", "health" })
        {
            using var request = new HttpRequestMessage(guard == "write" ? HttpMethod.Post : guard == "authorization" ? HttpMethod.Options : HttpMethod.Get,
                guard == "health" ? "/health" : "/v1/identity");
            request.Headers.Add("Origin", "http://localhost:4303");
            if (guard != "missing-header") request.Headers.Add("X-IAM-Agent", "1");
            if (guard == "foreign-host") request.Headers.Host = "attacker.example";
            if (guard == "authorization")
            {
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Access-Control-Request-Headers", "x-iam-agent,authorization");
            }
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Attestation_is_the_only_write_and_still_refuses_untrusted_origins()
    {
        var settings = Settings();
        await using var app = AgentWeb.Build(settings, new string('c', 64), 0);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };

        using var attest = new HttpRequestMessage(HttpMethod.Post, "/v1/attest")
        {
            Content = new StringContent("{\"nonce\":\"0123456789abcdef\"}", Encoding.UTF8, "application/json"),
        };
        attest.Headers.Add("Origin", settings.SiteOrigin);
        attest.Headers.Add("X-IAM-Agent", "1");
        using var attested = await client.SendAsync(attest, TestContext.Current.CancellationToken);
        // No installation directory in this host, so signing is unavailable rather than denied.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, attested.StatusCode);

        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/v1/attest");
        foreign.Headers.Add("Origin", "https://attacker.example");
        foreign.Headers.Add("X-IAM-Agent", "1");
        using var foreignDenied = await client.SendAsync(foreign, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, foreignDenied.StatusCode);

        using var unmarked = new HttpRequestMessage(HttpMethod.Post, "/v1/attest");
        unmarked.Headers.Add("Origin", settings.SiteOrigin);
        using var unmarkedDenied = await client.SendAsync(unmarked, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, unmarkedDenied.StatusCode);

        using var otherWrite = new HttpRequestMessage(HttpMethod.Post, "/health");
        otherWrite.Headers.Add("Origin", settings.SiteOrigin);
        otherWrite.Headers.Add("X-IAM-Agent", "1");
        using var otherDenied = await client.SendAsync(otherWrite, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, otherDenied.StatusCode);
    }
}
