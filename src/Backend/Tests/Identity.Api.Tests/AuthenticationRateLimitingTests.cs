using System.Net;
using System.Net.Http.Json;
using Identity.Contracts.Authentication;
using Identity.Contracts.Errors;

namespace Identity.Api.Tests;

public sealed class AuthenticationRateLimitingTests
{
    [Theory]
    [InlineData("/api/v1/auth/refresh", "/api/v1/auth/browser/refresh")]
    [InlineData("/api/v1/auth/browser/refresh", "/api/v1/auth/refresh")]
    public async Task RefreshBudget_IsSharedAcrossAliasesButIndependentOfLoginAndLogout(
        string refreshPath,
        string alternateRefreshPath)
    {
        await using var factory = new IdentityApiFactory(
            authenticationPermitLimit: 1,
            sessionRefreshPermitLimit: 2,
            sessionLogoutPermitLimit: 1);
        using var client = factory.CreateClient();
        using var first = await Post(client, refreshPath);
        using var second = await Post(client, refreshPath);
        using var limited = await Post(client, alternateRefreshPath);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, second.StatusCode);
        await AssertLimited(limited);

        using var login = await Post(client, "/api/v1/auth/browser/login");
        using var loginLimited = await Post(client, "/api/v1/auth/login");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        await AssertLimited(loginLimited);

        using var logout = await Post(client, "/api/v1/auth/browser/logout");
        using var logoutLimited = await Post(client, "/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        await AssertLimited(logoutLimited);
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/browser/login")]
    [InlineData("/api/v1/auth/mfa/complete")]
    [InlineData("/api/v1/auth/browser/mfa/complete")]
    [InlineData("/api/v1/auth/terminal/verify")]
    public async Task CredentialBudget_ProtectsAllVerificationRoutesWithoutBlockingSessions(string path)
    {
        await using var factory = new IdentityApiFactory(
            authenticationPermitLimit: 1,
            sessionRefreshPermitLimit: 1,
            sessionLogoutPermitLimit: 1);
        using var client = factory.CreateClient();
        using var login = await Post(client, "/api/v1/auth/login");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var limited = await Post(client, path);
        await AssertLimited(limited);

        using var refresh = await Post(client, "/api/v1/auth/refresh");
        using var logout = await Post(client, "/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/v1/auth/logout")]
    public async Task SessionBudget_UsesTrustedForwardedClientAddress(string path)
    {
        await using var factory = new IdentityApiFactory(
            authenticationPermitLimit: 1,
            reverseProxyAddress: "10.0.0.1",
            sessionRefreshPermitLimit: 1,
            sessionLogoutPermitLimit: 1);
        using var client = factory.CreateClient();
        using var first = await Post(client, path, "198.51.100.10");
        using var second = await Post(client, path, "198.51.100.11");
        using var limited = await Post(client, path, "198.51.100.10");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertLimited(limited);
    }

    [Theory]
    [InlineData(0, 30, 30)]
    [InlineData(10, 0, 30)]
    [InlineData(10, 1001, 30)]
    [InlineData(10, 30, 0)]
    [InlineData(10, 30, 1001)]
    public async Task InvalidLimits_FailHostStartup(int login, int refresh, int logout)
    {
        await using var factory = new IdentityApiFactory(
            authenticationPermitLimit: login,
            sessionRefreshPermitLimit: refresh,
            sessionLogoutPermitLimit: logout);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Identity:RateLimit:", exception.Message, StringComparison.Ordinal);
    }

    private static async Task AssertLimited(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = Assert.NotNull(response.Headers.RetryAfter?.Delta);
        Assert.InRange(retryAfter.TotalSeconds, 1, 60);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal("rate_limit_exceeded", body?.Code);
        Assert.True(Guid.TryParse(body?.CorrelationId, out _));
    }

    private static async Task<HttpResponseMessage> Post(
        HttpClient client,
        string path,
        string? address = null)
    {
        const string refreshToken = "fixture-refresh-token-with-at-least-32-characters";
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = path switch
            {
                "/api/v1/auth/browser/refresh" => JsonContent.Create(new BrowserRefreshRequest("client")),
                "/api/v1/auth/refresh" => JsonContent.Create(new RefreshTokenRequest(refreshToken, "client", null)),
                "/api/v1/auth/logout" or "/api/v1/auth/browser/logout" => JsonContent.Create(new LogoutRequest(refreshToken)),
                "/api/v1/auth/mfa/complete" or "/api/v1/auth/browser/mfa/complete" =>
                    JsonContent.Create(new CompleteMfaRequest(Guid.NewGuid(), "000000")),
                "/api/v1/auth/terminal/verify" =>
                    JsonContent.Create(new TerminalVerificationRequest(77, "unknown-user", "1234", "terminal", refreshToken)),
                _ => JsonContent.Create(new LoginRequest("unknown-user", "Fixture-only-password1!", "client", null, null)),
            },
        };
        request.Headers.Add("X-Identity-Session", "browser");
        if (address is not null)
        {
            request.Headers.Add("X-Forwarded-For", address);
            request.Headers.Add("X-Forwarded-Proto", "https");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
