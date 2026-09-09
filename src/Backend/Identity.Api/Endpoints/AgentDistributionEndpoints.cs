using System.Text.RegularExpressions;

namespace Identity.Api.Endpoints;

internal static class AgentDistributionEndpoints
{
    public static IEndpointRouteBuilder MapAgentDistributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/install.ps1", AgentInstallBootstrap.Serve).AllowAnonymous();
        endpoints.MapGet("/api/v1/agents/download", (IConfiguration config, HttpContext http) => Serve(config, http, "IAM.Agent.Setup.zip", true)).AllowAnonymous();
        endpoints.MapGet("/api/v1/agents/releases/{file}", (string file, IConfiguration config, HttpContext http) =>
        {
            if (file != "latest.json" && !Regex.IsMatch(file, @"^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$")) return Results.NotFound();
            return Serve(config, http, file, false);
        }).AllowAnonymous();
        return endpoints;
    }
    private static IResult Serve(IConfiguration config, HttpContext http, string file, bool download)
    {
        var root = config["AgentDistribution:RootPath"];
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return Results.NotFound();
        var path = Path.Combine(root, file);
        if (!File.Exists(path)) return Results.NotFound();
        http.Response.Headers.CacheControl = file == "latest.json" || download ? "no-store" : "public, max-age=86400, immutable";
        return Results.File(path, file.EndsWith(".json", StringComparison.Ordinal) ? "application/json" : "application/zip", download ? file : null, enableRangeProcessing: true);
    }
}
