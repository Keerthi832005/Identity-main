using Identity.Api.Administration;
using Identity.Api.Hosting;
using Identity.Api.Diagnostics;
using Identity.Api.RateLimiting;
using Identity.Application.Agents;
using Identity.Contracts.Agents;

namespace Identity.Api.Endpoints;

internal static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin/agents").RequireAuthorization(AdministrationPolicy.Name);
        admin.MapPost("/enroll", async (AgentEnrollment body, HttpContext http, IAgentRegistry registry, CancellationToken ct) =>
        {
            try { return Results.Ok(await registry.Enroll(body, RequestContextFactory.Administration(http), ct)); }
            catch (Exception e) when (e is ArgumentException or FormatException or System.Security.Cryptography.CryptographicException)
            { return Results.BadRequest(new { message = "Invalid or conflicting agent enrollment." }); }
        });
        admin.MapGet("", async (string? search, int? skip, int? take, IAgentRegistry registry, CancellationToken ct) =>
        {
            if ((search?.Length ?? 0) > 200 || skip is < 0 || take is < 1 or > 100) return Results.BadRequest();
            return Results.Ok(await registry.Search(search?.Trim(), skip ?? 0, take ?? 50, ct));
        });
        admin.MapGet("/{id:guid}", async (Guid id, IAgentRegistry registry, CancellationToken ct) =>
            await registry.Get(id, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        admin.MapPost("/{id:guid}/ping", async (Guid id, HttpContext http, IAgentRegistry registry,
            IMachinePingService ping, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var machine = await registry.Get(id, ct);
            if (machine is null) return Results.NotFound();
            if (!machine.Machine.IsActive) return Results.Conflict(new { message = "A revoked machine cannot be pinged." });
            return Results.Ok(await ping.Check(machine.Machine.Hostname, ct));
        }).RequireRateLimiting(RateLimitingExtensions.MachinePingPolicy);
        admin.MapGet("/{id:guid}/control", async (Guid id, HttpContext http, IAgentControlRegistry registry, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await registry.Get(id, ct) is { } result ? Results.Ok(result) : Results.NotFound();
        });
        admin.MapPost("/{id:guid}/update", async (Guid id, HttpContext http, IAgentControlRegistry registry, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await registry.RequestUpdate(id, RequestContextFactory.Administration(http), ct) is { } result
                ? Results.Ok(result)
                : Results.Conflict(new { message = "An active machine with IAM.Agent supervisor 1.0.1 or later is required. Refresh machine status first." });
        }).RequireRateLimiting(RateLimitingExtensions.MachinePingPolicy);
        admin.MapPost("/{id:guid}/collect", async (Guid id, HttpContext http, IAgentControlRegistry registry, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await registry.RequestCollect(id, RequestContextFactory.Administration(http), ct) is { } result
                ? Results.Ok(result)
                : Results.Conflict(new { message = "This machine's agent cannot collect on demand. Update the agent, then refresh machine status." });
        }).RequireRateLimiting(RateLimitingExtensions.MachinePingPolicy);
        endpoints.MapPost("/api/v1/agents/control", async (SignedMachineReport report, HttpContext http,
            IAgentControlRegistry registry, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await registry.Poll(report, ct) is { } result ? Results.Ok(result) : Results.Unauthorized();
        }).AllowAnonymous();
        endpoints.MapPost("/api/v1/agents/collect", async (SignedMachineReport report, HttpContext http,
            IAgentControlRegistry registry, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await registry.PollCollect(report, ct) is { } result ? Results.Ok(result) : Results.Unauthorized();
        }).AllowAnonymous();
        // Does not accept an IAM bearer token: each device proves its own registered key.
        endpoints.MapPost("/api/v1/agents/report", async (SignedMachineReport report, IAgentRegistry registry, CancellationToken ct) =>
            await registry.Report(report, ct) ? Results.NoContent() : Results.Unauthorized()).AllowAnonymous();
        return endpoints;
    }
}
