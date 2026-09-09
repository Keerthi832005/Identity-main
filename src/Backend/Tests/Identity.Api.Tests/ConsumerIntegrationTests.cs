using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Identity.Application.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Tests;

public sealed class ConsumerIntegrationTests(IdentityApiFactory identityFactory)
    : IClassFixture<IdentityApiFactory>
{
    private const string ConsumerAudience = "pts-api";
    private const string RequiredCapability = "pts.api";

    [Fact]
    public async Task Consumer_AcceptsIamTokenThroughAuthorityMetadataAndJwks()
    {
        var token = IssueToken(ConsumerAudience, [RequiredCapability]);
        await using var consumer = await ConsumerHost.Start(identityFactory);

        using var response = await consumer.GetProtected(token);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Consumer returned {response.StatusCode}: {response.Headers.WwwAuthenticate}");
    }

    [Fact]
    public async Task Consumer_RejectsMissingCapabilityWrongAudienceAndExpiredToken()
    {
        var missingCapability = IssueToken(ConsumerAudience, ["pts.other"]);
        var wrongAudience = IssueToken("other-api", [RequiredCapability]);
        var expired = IssueToken(
            ConsumerAudience,
            [RequiredCapability],
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5));
        await using var consumer = await ConsumerHost.Start(identityFactory);

        using var forbidden = await consumer.GetProtected(missingCapability);
        using var invalidAudience = await consumer.GetProtected(wrongAudience);
        using var stale = await consumer.GetProtected(expired);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
    }

    private string IssueToken(
        string audience,
        IReadOnlyList<string> capabilityCodes,
        DateTime? issuedAt = null,
        DateTime? expiresAt = null)
    {
        using var scope = identityFactory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        var issued = issuedAt ?? DateTime.UtcNow.AddMinutes(-1);
        return issuer.Issue(new AccessTokenRequest(
            42,
            "EMP-042",
            "Employee Forty Two",
            7,
            audience,
            "consumer-integration",
            3,
            5,
            capabilityCodes,
            issued,
            expiresAt ?? DateTime.UtcNow.AddMinutes(5))).Token;
    }

    private sealed class ConsumerHost(WebApplication application) : IAsyncDisposable
    {
        private readonly HttpClient client = application.GetTestClient();

        public static async Task<ConsumerHost> Start(IdentityApiFactory identityFactory)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = IdentityApiFactory.Issuer;
                    options.Audience = ConsumerAudience;
                    options.RequireHttpsMetadata = true;
                    options.MapInboundClaims = false;
                    options.BackchannelHttpHandler = identityFactory.Server.CreateHandler();
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "sub",
                        ClockSkew = TimeSpan.Zero,
                    };
                });
            builder.Services.AddAuthorization(options => options.AddPolicy(
                RequiredCapability,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim("capability", RequiredCapability)
                    .RequireClaim("security_version")
                    .RequireClaim("authorization_version")));
            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapGet("/protected", (ClaimsPrincipal user) => Results.Ok(
                new ConsumerIdentity(
                    user.FindFirst("sub")?.Value ?? string.Empty,
                    int.Parse(
                        user.FindFirst("security_version")?.Value ?? "0",
                        CultureInfo.InvariantCulture),
                    int.Parse(
                        user.FindFirst("authorization_version")?.Value ?? "0",
                        CultureInfo.InvariantCulture))))
                .RequireAuthorization(RequiredCapability);
            await application.StartAsync(TestContext.Current.CancellationToken);
            return new ConsumerHost(application);
        }

        public Task<HttpResponseMessage> GetProtected(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client.SendAsync(
                request,
                TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await application.DisposeAsync();
        }
    }

    private sealed record ConsumerIdentity(
        string Subject,
        int SecurityVersion,
        int AuthorizationVersion);
}
