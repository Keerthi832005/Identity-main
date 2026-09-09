using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Api.Health;
using Identity.Application.BulkData;
using Identity.Application.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Tests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly int authenticationPermitLimit;
    private readonly int sessionRefreshPermitLimit;
    private readonly int sessionLogoutPermitLimit;
    private readonly IPAddress? reverseProxyAddress;

    public IdentityApiFactory()
        : this(100)
    {
    }

    internal IdentityApiFactory(
        int authenticationPermitLimit,
        string? reverseProxyAddress = null,
        int sessionRefreshPermitLimit = 100,
        int sessionLogoutPermitLimit = 100)
    {
        this.authenticationPermitLimit = authenticationPermitLimit;
        this.sessionRefreshPermitLimit = sessionRefreshPermitLimit;
        this.sessionLogoutPermitLimit = sessionLogoutPermitLimit;
        this.reverseProxyAddress = reverseProxyAddress is null
            ? null
            : IPAddress.Parse(reverseProxyAddress);
    }

    public const string Issuer = "https://identity.test";
    public const string AdministrationAudience = "urn:identity:administration";
    public FakeRequestDispatcher Dispatcher { get; private set; } = null!;

    public string CreateAccessToken(
        bool includeAdministrationCapability = true,
        string audience = AdministrationAudience,
        long subject = 42)
    {
        var claims = new List<Claim> { new("sub", subject.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        if (includeAdministrationCapability)
        {
            claims.Add(new Claim("capability", "iam.admin"));
        }

        var token = new JwtSecurityToken(
            Issuer,
            audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(
                new RsaSecurityKey(signingKey) { KeyId = "test-key" },
                SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateKeyPem = signingKey.ExportPkcs8PrivateKeyPem();
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Identity"] =
                "Server=(localdb)\\mssqllocaldb;Database=Identity_ApiTests;Integrated Security=true",
            ["Identity:Jwt:Issuer"] = Issuer,
            ["Identity:Jwt:KeyId"] = "test-key",
            ["Identity:Jwt:PrivateKeyPem"] = privateKeyPem,
            ["Identity:Jwt:AdministrationAudience"] = AdministrationAudience,
            ["Identity:Security:KeyId"] = "test-protection-key",
            ["Identity:Security:EncryptionKey"] = Key(1),
            ["Identity:Security:ChallengeKey"] = Key(2),
            ["Identity:Security:IdentifierHashKey"] = Key(3),
            ["Identity:RateLimit:AuthenticationPermitLimit"] =
                authenticationPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Identity:RateLimit:SessionRefreshPermitLimit"] =
                sessionRefreshPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Identity:RateLimit:SessionLogoutPermitLimit"] =
                sessionLogoutPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Identity:BrowserSession:CookieName"] = "identity-refresh",
            ["Identity:BrowserSession:CookiePath"] = "/identity",
            ["Identity:BrowserSession:RequireSecureCookie"] = "true",
            ["AllowedHosts"] = "identity.test;localhost;127.0.0.1",
        };
        if (reverseProxyAddress is not null)
        {
            settings["ReverseProxy:KnownProxies:0"] = reverseProxyAddress.ToString();
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRequestDispatcher>();
            services.RemoveAll<IReadinessProbe>();
            var parameters = signingKey.ExportParameters(false);
            Dispatcher = new FakeRequestDispatcher(
                Base64UrlEncoder.Encode(parameters.Modulus),
                Base64UrlEncoder.Encode(parameters.Exponent));
            services.AddSingleton<IRequestDispatcher>(Dispatcher);
            services.AddSingleton<IReadinessProbe, ReadyProbe>();
            if (reverseProxyAddress is not null)
            {
                services.AddSingleton<IStartupFilter>(
                    new RemoteAddressStartupFilter(reverseProxyAddress));
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            signingKey.Dispose();
        }
    }

    private static string Key(byte value) => Convert.ToBase64String(
        Enumerable.Repeat(value, 32).ToArray());

    private sealed class ReadyProbe : IReadinessProbe
    {
        public Task<bool> IsReady(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class RemoteAddressStartupFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = address;
                await following();
            });
            next(app);
        };
    }
}
