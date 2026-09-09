using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using IAM.Agent.Core;
using Identity.Contracts.Agents;

namespace IAM.Agent;

public static class AgentWeb
{
    public static string Version => typeof(AgentWeb).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];

    public static WebApplication Build(AgentSettings settings, string healthToken, int port = AgentSettings.Port, string? directory = null)
    {
        settings.Validate();
        if (healthToken.Length < 32) throw new ArgumentException("A random supervisor token is required.", nameof(healthToken));
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        if (directory is not null)
        {
            builder.Services.AddSingleton(new MachineReporter(settings, directory));
            builder.Services.AddHostedService(provider => provider.GetRequiredService<MachineReporter>());
        }
        builder.Logging.ClearProviders(); // Requests may carry supervisor tokens: never log headers.
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, port);
            server.Limits.MaxRequestBodySize = 1024;
            server.Limits.MaxConcurrentConnections = 32;
            server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var actualPort = context.Connection.LocalPort;
            if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip)
                || request.Host.Value != $"127.0.0.1:{actualPort}")
            {
                context.Response.StatusCode = 403;
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (request.Path == "/health" && request.Method == "GET")
            {
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(request.Headers["X-IAM-Health"].ToString()), Encoding.UTF8.GetBytes(healthToken)))
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                await next(context);
                return;
            }
            var origin = request.Headers.Origin.ToString();
            // Attestation is the only write the browser may make; identity stays read-only.
            var attestation = request.Path == "/v1/attest";
            if ((request.Path != "/v1/identity" && !attestation) || !settings.AllowsOrigin(origin))
            {
                context.Response.StatusCode = 403;
                return;
            }
            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.Vary = "Origin";
            if (request.Method == "OPTIONS")
            {
                var methods = attestation ? "POST" : "GET";
                var headers = attestation ? "X-IAM-Agent, Content-Type" : "X-IAM-Agent";
                if (request.Headers.AccessControlRequestMethod != methods
                    || request.Headers.AccessControlRequestHeaders.ToString().Split(',', StringSplitOptions.TrimEntries)
                        .Any(h => !h.Equals("x-iam-agent", StringComparison.OrdinalIgnoreCase)
                            && !(attestation && h.Equals("content-type", StringComparison.OrdinalIgnoreCase))))
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                context.Response.Headers.AccessControlAllowMethods = methods;
                context.Response.Headers.AccessControlAllowHeaders = headers;
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                context.Response.StatusCode = 204;
                return;
            }
            var expected = attestation ? "POST" : "GET";
            if (request.Method != expected || request.Headers["X-IAM-Agent"] != "1")
            {
                context.Response.StatusCode = 403;
                return;
            }
            await next(context);
        });
        app.MapGet("/v1/identity", (HttpRequest request) => Results.Ok(new
        {
            protocolVersion = 1,
            terminalId = settings.TerminalId,
            hostname = Environment.MachineName,
            // The middleware has approved this exact browser origin. The registered
            // HTTPS site and signed update feed remain unchanged in AgentSettings.
            siteOrigin = request.Headers.Origin.ToString(),
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.OSArchitecture.ToString(),
            agentVersion = Version,
        }));
        app.MapPost("/v1/attest", (AgentAttestationBody body) =>
            directory is null || !OperatingSystem.IsWindows()
                ? Results.StatusCode(503)
                : Attest(directory, body.Nonce ?? string.Empty));
        app.MapGet("/health", () => Results.Ok(new { status = "ready", version = Version, processId = Environment.ProcessId }));
        return app;
    }
    // Signs an IAM-issued nonce; the device key never leaves this machine.
    [SupportedOSPlatform("windows")]
    private static IResult Attest(string directory, string nonce)
    {
        var enrollment = DeviceKey.CreateOrRead(directory);
        byte[] payload;
        try
        {
            payload = AgentProtocol.AttestationPayload(enrollment.InstallationId, nonce);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { error = "Invalid attestation nonce." });
        }

        using var key = DeviceKey.Read(directory);
        return Results.Ok(new
        {
            protocolVersion = 1,
            installationId = enrollment.InstallationId,
            signature = Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation)),
        });
    }

}

public sealed record AgentAttestationBody(string? Nonce);
