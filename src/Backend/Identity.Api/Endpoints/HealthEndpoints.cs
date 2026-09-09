using Identity.Api.Health;

namespace Identity.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapIdentityHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => new HealthStatusResponse("Healthy"))
            .AllowAnonymous();
        endpoints.MapGet("/health/ready", async (
            IReadinessProbe probe,
            CancellationToken cancellationToken) =>
        {
            var isReady = await probe.IsReady(cancellationToken);
            return Results.Json(
                new HealthStatusResponse(isReady ? "Healthy" : "Unhealthy"),
                statusCode: isReady
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();
        return endpoints;
    }
}
