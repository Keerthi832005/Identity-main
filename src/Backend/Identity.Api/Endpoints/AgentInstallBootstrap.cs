using System.Security.Cryptography;

namespace Identity.Api.Endpoints;

internal static class AgentInstallBootstrap
{
    private const long MaximumSetupBytes = 512L * 1024 * 1024;
    private static readonly object Gate = new();
    private static (string Path, long Length, DateTime Modified, string Hash)? cached;
    private static readonly Lazy<string> Template = new(() =>
    {
        using var stream = typeof(AgentInstallBootstrap).Assembly.GetManifestResourceStream(
            "Identity.Api.AgentInstallBootstrap.ps1")
            ?? throw new InvalidOperationException("Agent bootstrap resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static IResult Serve(IConfiguration config, HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        var root = config["AgentDistribution:RootPath"];
        // Use configured public identity URL, never the caller-controlled Host header.
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)
            || !Uri.TryCreate(config["Identity:Jwt:Issuer"], UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps || issuer.UserInfo.Length != 0
            || issuer.Query.Length != 0 || issuer.Fragment.Length != 0)
        {
            return Results.NotFound();
        }

        var setup = new FileInfo(Path.Combine(root, "IAM.Agent.Setup.zip"));
        if (!setup.Exists || setup.Length is <= 0 or > MaximumSetupBytes)
        {
            return Results.NotFound();
        }

        string hash;
        lock (Gate)
        {
            if (cached is { } previous && previous.Path == setup.FullName
                && previous.Length == setup.Length && previous.Modified == setup.LastWriteTimeUtc)
            {
                hash = previous.Hash;
            }
            else
            {
                using var stream = setup.OpenRead();
                hash = Convert.ToHexString(SHA256.HashData(stream));
                cached = (setup.FullName, setup.Length, setup.LastWriteTimeUtc, hash);
            }
        }

        var download = new Uri(issuer.AbsoluteUri.TrimEnd('/') + "/api/v1/agents/download");
        var script = Template.Value
            .Replace("__SETUP_URL__", download.AbsoluteUri.Replace("'", "''", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("__SETUP_SHA256__", hash, StringComparison.Ordinal);
        return Results.Text(script, "text/plain; charset=utf-8");
    }
}
