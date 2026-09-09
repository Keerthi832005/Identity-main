using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Api.Diagnostics;
using Identity.Application.Administration;
using Identity.Application.Agents;
using Identity.Contracts.Agents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Api.Tests;

public sealed class MachinePingTests
{
    private static readonly Guid MachineId = Guid.Parse("81111111-1111-4111-8111-111111111111");
    private static readonly Guid RevokedId = Guid.Parse("82222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task Endpoint_requires_admin_and_active_enrollment_and_never_uses_a_request_target()
    {
        await using var factory = new IdentityApiFactory();
        var probe = new FakeTransport();
        await using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAgentRegistry>(); s.AddSingleton<IAgentRegistry, Registry>();
            s.RemoveAll<IMachinePingTransport>(); s.AddSingleton<IMachinePingTransport>(probe);
        }));
        using var client = app.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var url = $"/api/v1/admin/agents/{MachineId}/ping";
        using var anonymous = await client.PostAsJsonAsync(url, new { hostname = "external.example" }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
        using var forbidden = await client.PostAsJsonAsync(url, new { }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        using var revoked = await client.PostAsJsonAsync($"/api/v1/admin/agents/{RevokedId}/ping", new { }, ct);
        Assert.Equal(HttpStatusCode.Conflict, revoked.StatusCode);
        using var missing = await client.PostAsJsonAsync($"/api/v1/admin/agents/{Guid.NewGuid()}/ping", new { }, ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(probe.Targets);
        using var allowed = await client.PostAsJsonAsync(url + "?hostname=external.example", new { hostname = "127.0.0.1" }, ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True(allowed.Headers.CacheControl?.NoStore);
        Assert.Equal("MANAGED-PC", Assert.Single(probe.Targets));
        var result = await allowed.Content.ReadFromJsonAsync<MachinePingResult>(ct);
        Assert.Equal("reachable", result!.Status);
        Assert.Equal(7, result.RoundTripMilliseconds);
        for (var i = 0; i < 8; i++)
        {
            using var attempt = await client.PostAsJsonAsync(url, new { }, ct);
            if (attempt.StatusCode == HttpStatusCode.TooManyRequests)
            { Assert.NotNull(attempt.Headers.RetryAfter); return; }
        }
        Assert.Fail("Expected the bounded per-client ping budget to reject repeated requests.");
    }

    [Theory]
    [InlineData("https://host/path")]
    [InlineData("HOST\n")]
    [InlineData("HOST;command")]
    [InlineData("127.0.0.1")]
    [InlineData("2130706433")]
    [InlineData("localhost")]
    [InlineData("other.example.com")]
    public async Task Unsupported_targets_never_reach_the_transport(string hostname)
    {
        var transport = new FakeTransport();
        using var service = new MachinePingService(transport);
        Assert.Equal("unsupportedHostname", (await service.Check(hostname, TestContext.Current.CancellationToken)).Status);
        Assert.Empty(transport.Targets);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("ff02::1")]
    public void Resolver_cannot_select_loopback_unspecified_or_multicast(string address) =>
        Assert.False(SystemMachinePingTransport.IsUnicastTarget(IPAddress.Parse(address)));

    [Fact]
    public async Task Excess_concurrency_is_rejected_without_queueing_and_slots_are_released()
    {
        var gate = new TaskCompletionSource<MachinePingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport { Pending = gate.Task };
        using var service = new MachinePingService(transport);
        var ct = TestContext.Current.CancellationToken;
        var active = Enumerable.Range(0, 4).Select(_ => service.Check("MANAGED-PC", ct)).ToArray();
        Assert.Equal("busy", (await service.Check("ANOTHER-PC", ct)).Status);
        Assert.Equal(4, transport.Targets.Count);
        gate.SetResult(new(DateTimeOffset.UtcNow, "noReply"));
        await Task.WhenAll(active);
        Assert.Equal("noReply", (await service.Check("MANAGED-PC", ct)).Status);
    }

    private sealed class FakeTransport : IMachinePingTransport
    {
        public List<string> Targets { get; } = [];
        public Task<MachinePingResult>? Pending { get; init; }
        public Task<MachinePingResult> Check(string hostname, CancellationToken ct)
        {
            Targets.Add(hostname);
            return Pending ?? Task.FromResult(new MachinePingResult(DateTimeOffset.UtcNow, "reachable", "192.0.2.1", 7));
        }
    }
    private sealed class Registry : IAgentRegistry
    {
        public Task<AgentDetails?> Get(Guid id, CancellationToken ct) => Task.FromResult<AgentDetails?>(
            id != MachineId && id != RevokedId ? null : new(new(id, 1, "MANAGED-PC", "1.0.1", DateTime.UtcNow, null,
                id != RevokedId, true, DateTime.UtcNow.AddDays(1)), null));
        public Task<AgentPage> Search(string? search, int skip, int take, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> Report(SignedMachineReport report, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgentEnrollmentResult> Enroll(AgentEnrollment request, AdministrationContext context, CancellationToken ct) => throw new NotSupportedException();
    }
}
