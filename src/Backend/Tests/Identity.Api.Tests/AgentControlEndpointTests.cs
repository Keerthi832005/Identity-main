using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Administration;
using Identity.Application.Agents;
using Identity.Contracts.Agents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Api.Tests;

public sealed class AgentControlEndpointTests
{
    [Fact]
    public async Task Update_requests_require_admin_and_use_only_the_route_machine_and_server_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory();
        var registry = new Registry();
        await using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        { s.RemoveAll<IAgentControlRegistry>(); s.AddSingleton<IAgentControlRegistry>(registry); }));
        using var client = app.CreateClient();
        var path = $"/api/v1/admin/agents/{registry.Id}";
        using var anonymous = await client.PostAsJsonAsync(path + "/update", new { }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
        using var forbidden = await client.PostAsJsonAsync(path + "/update", new { }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, registry.Requests);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        using var queued = await client.PostAsJsonAsync(path + "/update", new { url = "https://untrusted.test", installationId = Guid.NewGuid() }, ct);
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        Assert.True(queued.Headers.CacheControl?.NoStore);
        Assert.Equal(registry.Id, registry.RequestedId);
        Assert.True(registry.Actor!.ActorUserId > 0);
        using var missing = await client.GetAsync($"/api/v1/admin/agents/{Guid.NewGuid()}/control", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var unsupported = await client.PostAsJsonAsync($"/api/v1/admin/agents/{Guid.NewGuid()}/update", new { }, ct);
        Assert.Equal(HttpStatusCode.Conflict, unsupported.StatusCode);
        // Even an administrator token cannot substitute for the registered device's signed proof.
        using var badProof = await client.PostAsJsonAsync("/api/v1/agents/control", new SignedMachineReport(registry.Id, "e30=", "invalid"), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, badProof.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var noAuthStatus = await client.GetAsync(path + "/control", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, noAuthStatus.StatusCode);
    }

    [Fact]
    public async Task Collect_requests_require_admin_and_report_an_agent_that_cannot_collect()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new IdentityApiFactory();
        var registry = new Registry();
        await using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        { s.RemoveAll<IAgentControlRegistry>(); s.AddSingleton<IAgentControlRegistry>(registry); }));
        using var client = app.CreateClient();
        var path = $"/api/v1/admin/agents/{registry.Id}/collect";
        using var anonymous = await client.PostAsJsonAsync(path, new { }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
        using var forbidden = await client.PostAsJsonAsync(path, new { }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, registry.Collections);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        using var queued = await client.PostAsJsonAsync(path, new { installationId = Guid.NewGuid() }, ct);
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        Assert.True(queued.Headers.CacheControl?.NoStore);
        Assert.Equal(registry.Id, registry.CollectedId);
        Assert.True(registry.Actor!.ActorUserId > 0);
        using var unsupported = await client.PostAsJsonAsync($"/api/v1/admin/agents/{Guid.NewGuid()}/collect", new { }, ct);
        Assert.Equal(HttpStatusCode.Conflict, unsupported.StatusCode);
    }

    private sealed class Registry : IAgentControlRegistry
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid RequestedId;
        public int Requests;
        public AdministrationContext? Actor;
        public Task<AgentControlStatus?> Get(Guid id, CancellationToken ct) => Task.FromResult<AgentControlStatus?>(id == Id ? new(null, null, null, null) : null);
        public Task<AgentUpdateStatus?> RequestUpdate(Guid id, AdministrationContext actor, CancellationToken ct)
        {
            Requests++; RequestedId = id; Actor = actor;
            return Task.FromResult<AgentUpdateStatus?>(id == Id ? new(Guid.NewGuid(), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(15), null, null, "queued", null) : null);
        }
        public Guid CollectedId;
        public int Collections;
        public Task<AgentUpdateStatus?> RequestCollect(Guid id, AdministrationContext actor, CancellationToken ct)
        {
            Collections++; CollectedId = id; Actor = actor;
            return Task.FromResult<AgentUpdateStatus?>(id == Id ? new(Guid.NewGuid(), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5), null, null, "queued", null) : null);
        }
        public Task<AgentControlReply?> Poll(SignedMachineReport report, CancellationToken ct) => Task.FromResult<AgentControlReply?>(null);
        public Task<AgentCollectReply?> PollCollect(SignedMachineReport report, CancellationToken ct) => Task.FromResult<AgentCollectReply?>(null);
    }
}
