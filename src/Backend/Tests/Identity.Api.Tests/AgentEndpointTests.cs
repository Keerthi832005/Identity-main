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

public sealed class AgentEndpointTests
{
    [Fact]
    public async Task Inventory_and_enrollment_are_admin_only_and_reports_use_device_proof()
    {
        await using var factory = new IdentityApiFactory();
        await using var configured = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IAgentRegistry>(); s.AddSingleton<IAgentRegistry, TestRegistry>(); }));
        using var client = configured.CreateClient();
        using var denied = await client.GetAsync("/api/v1/admin/agents", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
        using var forbidden = await client.GetAsync("/api/v1/admin/agents", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        using var allowed = await client.GetAsync("/api/v1/admin/agents", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var badReport = await client.PostAsJsonAsync("/api/v1/agents/report", new SignedMachineReport(Guid.NewGuid(), "e30=", "invalid"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, badReport.StatusCode);
    }
    private sealed class TestRegistry : IAgentRegistry
    {
        public Task<AgentEnrollmentResult> Enroll(AgentEnrollment request, AdministrationContext context, CancellationToken ct) => Task.FromResult(new AgentEnrollmentResult(request.InstallationId, 1, true));
        public Task<bool> Report(SignedMachineReport report, CancellationToken ct) => Task.FromResult(false);
        public Task<AgentPage> Search(string? search, int skip, int take, CancellationToken ct) => Task.FromResult(new AgentPage([], 0, skip, take));
        public Task<AgentDetails?> Get(Guid id, CancellationToken ct) => Task.FromResult<AgentDetails?>(null);
    }
}
