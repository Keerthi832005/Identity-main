using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Contracts.Administration;
using Identity.Contracts.Authentication;
using Identity.Contracts.Discovery;
using Identity.Contracts.Errors;

namespace Identity.Api.Tests;

public sealed class ApiSecurityTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task DiscoveryEndpoints_ReturnTypedIssuerAndPublicJwk()
    {
        using var client = factory.CreateClient();

        var standardDiscovery = await client.GetFromJsonAsync<OpenIdConfigurationResponse>(
            "/.well-known/openid-configuration",
            TestContext.Current.CancellationToken);
        var discovery = await client.GetFromJsonAsync<IdentityDiscoveryResponse>(
            "/.well-known/identity-configuration",
            TestContext.Current.CancellationToken);
        var keys = await client.GetFromJsonAsync<JsonWebKeySetResponse>(
            "/.well-known/jwks.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityApiFactory.Issuer, standardDiscovery?.Issuer);
        Assert.EndsWith(
            "/.well-known/jwks.json",
            standardDiscovery?.JwksUri,
            StringComparison.Ordinal);
        Assert.Contains("RS256", standardDiscovery!.SigningAlgorithmsSupported);
        Assert.Equal(IdentityApiFactory.Issuer, discovery?.Issuer);
        Assert.EndsWith("/.well-known/jwks.json", discovery?.JwksUri, StringComparison.Ordinal);
        var key = Assert.Single(keys!.Keys);
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("RS256", key.Alg);
        Assert.NotEmpty(key.N);
        Assert.NotEmpty(key.E);
    }

    [Fact]
    public async Task Login_ValidatesBoundsMapsOutcomesAndDoesNotEchoSecrets()
    {
        using var client = factory.CreateClient();
        const string password = "Secret-Horse-Battery-Staple-42";
        using var invalidRequest = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("employee", "short", "client", null, null),
            TestContext.Current.CancellationToken);
        using var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("unknown-user", password, "client", null, null),
            TestContext.Current.CancellationToken);
        using var issued = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("valid-user", password, "client", null, null),
            TestContext.Current.CancellationToken);
        var issuedBody = await issued.Content.ReadFromJsonAsync<AuthenticationResponse>(
            TestContext.Current.CancellationToken);
        var rejectedText = await rejected.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidRequest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.DoesNotContain(password, rejectedText, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        Assert.True(issuedBody?.Succeeded);
        Assert.Equal("signed-access-token", issuedBody?.AccessToken);
        Assert.Equal("no-store", issued.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(issued.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Login_ReturnsAcceptedMfaChallengeWithoutTokens()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(
                "mfa-user",
                "Secret-Horse-Battery-Staple-42",
                "client",
                null,
                null),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body?.MfaChallengeId);
        Assert.Null(body?.AccessToken);
        Assert.Null(body?.RefreshToken);
    }

    [Fact]
    public async Task TerminalVerification_UsesTypedContractWithoutEchoingPinOrClientSecret()
    {
        using var client = factory.CreateClient();
        const string pin = "4682";
        const string clientSecret = "terminal-client-secret-with-enough-entropy";
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/terminal/verify",
            new TerminalVerificationRequest(
                77, "valid-terminal", pin, "pts-terminal-service", clientSecret),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<TerminalVerificationResponse>(
            TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body?.Succeeded);
        Assert.Equal(42, body?.UserId);
        Assert.Equal(["pts.shopfloor.supervise"], body?.CapabilityCodes);
        Assert.DoesNotContain(pin, text, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, text, StringComparison.Ordinal);
        var command = Assert.IsType<TerminalVerificationCommand>(factory.Dispatcher.LastRequest);
        Assert.Equal(77, command.TerminalId);
    }

    [Fact]
    public async Task BrowserSession_UsesHttpOnlyCookieForLoginRefreshMfaAndLogout()
    {
        using var client = factory.CreateClient();
        var login = new LoginRequest(
            "valid-user",
            "Secret-Horse-Battery-Staple-42",
            "client",
            null,
            null);
        using var missingHeader = await client.PostAsJsonAsync(
            "/api/v1/auth/browser/login",
            login,
            TestContext.Current.CancellationToken);
        using var loginResponse = await client.SendAsync(
            BrowserPost("/api/v1/auth/browser/login", login),
            TestContext.Current.CancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(
            TestContext.Current.CancellationToken);
        var cookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));

        Assert.Equal(HttpStatusCode.Forbidden, missingHeader.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal("signed-access-token", loginBody?.AccessToken);
        Assert.Null(loginBody?.RefreshToken);
        Assert.Contains("identity-refresh=opaque-refresh-token-value-with-required-length", cookie,
            StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/identity", cookie, StringComparison.OrdinalIgnoreCase);

        using var refreshResponse = await client.SendAsync(
            BrowserPost(
                "/api/v1/auth/browser/refresh",
                new BrowserRefreshRequest("client"),
                "identity-refresh=opaque-refresh-token-value-with-required-length"),
            TestContext.Current.CancellationToken);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal("signed-access-token", refreshBody?.AccessToken);
        Assert.Null(refreshBody?.RefreshToken);

        using var mfaResponse = await client.SendAsync(
            BrowserPost(
                "/api/v1/auth/browser/mfa/complete",
                new CompleteMfaRequest(
                    Guid.Parse("33445566-7788-4990-aabb-ccddeeff0011"),
                    "123456")),
            TestContext.Current.CancellationToken);
        var mfaBody = await mfaResponse.Content.ReadFromJsonAsync<AuthenticationResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, mfaResponse.StatusCode);
        Assert.Null(mfaBody?.RefreshToken);
        Assert.Contains(mfaResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase));

        using var logoutRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/auth/browser/logout");
        logoutRequest.Headers.Add("X-Identity-Session", "browser");
        logoutRequest.Headers.Add(
            "Cookie",
            "identity-refresh=opaque-refresh-token-value-with-required-length");
        using var logoutResponse = await client.SendAsync(
            logoutRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.Contains(logoutResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("identity-refresh=", StringComparison.Ordinal)
            && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Administration_RequiresValidAudienceAndCapability()
    {
        using var client = factory.CreateClient();
        var request = new CreateUserRequest("API-100", "API User", "user@example.com", 7);

        using var anonymous = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            request,
            TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken(
            includeAdministrationCapability: false));
        using var missingCapability = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            request,
            TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken(
            audience: "urn:identity:wrong-audience"));
        using var wrongAudience = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            request,
            TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken());
        using var authorized = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            request,
            TestContext.Current.CancellationToken);
        var body = await authorized.Content.ReadFromJsonAsync<AdministrationResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missingCapability.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Created, authorized.StatusCode);
        Assert.Equal(123, body?.ResourceId);
        var command = Assert.IsType<CreateUserCommand>(factory.Dispatcher.LastRequest);
        Assert.Equal(42, command.Context.ActorUserId);
        Assert.Equal("user@example.com", command.Email);
        Assert.Equal(7, command.ManagerUserId);
    }

    [Fact]
    public async Task UserProfileUpdate_IsAuthorizedValidatedAndTyped()
    {
        using var client = factory.CreateClient();
        var request = new UpdateUserProfileRequest("Updated user", "updated@example.com", 7,
            new UserOrganizationMappingRequest(11, 12));
        using var anonymous = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile", request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken(includeAdministrationCapability: false));
        using var denied = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile", request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken());
        using var invalid = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile",
            request with { Email = "invalid", ManagerUserId = -1 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var updated = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile", request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var command = Assert.IsType<UpdateUserProfileCommand>(factory.Dispatcher.LastRequest);
        Assert.Equal(123, command.UserId);
        Assert.Equal(new UserOrganizationMapping(11, 12), command.OrganizationMapping);
        foreach (var invalidMapping in new[] { new UserOrganizationMappingRequest(-1, null), new UserOrganizationMappingRequest(null, 12) })
        {
            using var invalidMappingResponse = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile",
                request with { OrganizationMapping = invalidMapping }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalidMappingResponse.StatusCode);
        }
        using var legacy = await client.PutAsJsonAsync("/api/v1/admin/users/123/profile",
            new { DisplayName = "Old client", Email = (string?)null, ManagerUserId = (long?)null }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Null(Assert.IsType<UpdateUserProfileCommand>(factory.Dispatcher.LastRequest).OrganizationMapping);
        Assert.Equal(42, command.Context.ActorUserId);
        Assert.Equal("updated@example.com", command.Email);
        Assert.Equal(7, command.ManagerUserId);
    }

    [Fact]
    public async Task AdministrationDiscovery_IsProtectedBoundedAndTyped()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync(
            "/api/v1/admin/dashboard",
            TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = Bearer(factory.CreateAccessToken());
        var dashboard = await client.GetFromJsonAsync<AdministrationDashboardResponse>(
            "/api/v1/admin/dashboard?recent=5",
            TestContext.Current.CancellationToken);
        var applications = await client.GetFromJsonAsync<PagedApplicationsResponse>(
            "/api/v1/admin/applications?search=identity&skip=0&take=10",
            TestContext.Current.CancellationToken);
        var users = await client.GetFromJsonAsync<PagedUsersResponse>(
            "/api/v1/admin/users?search=admin&skip=0&take=10",
            TestContext.Current.CancellationToken);
        using var catalogResponse = await client.GetAsync(
            "/api/v1/admin/applications/10/catalog",
            TestContext.Current.CancellationToken);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<ApplicationCatalogResponse>(
            TestContext.Current.CancellationToken);
        var catalogJson = await catalogResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var userAccess = await client.GetFromJsonAsync<UserAccessCatalogResponse>(
            "/api/v1/admin/users/42/access",
            TestContext.Current.CancellationToken);
        var applicationAccess = await client.GetFromJsonAsync<ApplicationAccessCatalogResponse>(
            "/api/v1/admin/applications/10/access",
            TestContext.Current.CancellationToken);
        using var securityResponse = await client.GetAsync(
            "/api/v1/admin/users/42/security",
            TestContext.Current.CancellationToken);
        var security = await securityResponse.Content.ReadFromJsonAsync<UserSecurityCatalogResponse>(
            TestContext.Current.CancellationToken);
        var securityJson = await securityResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(1, dashboard?.ApplicationCount);
        Assert.Equal(2, dashboard?.ActiveSessionCount);
        Assert.Equal("Identity Administration", Assert.Single(dashboard!.RecentApplications).ApplicationName);
        Assert.Equal("LoginSucceeded", Assert.Single(dashboard.RecentAuditEvents).EventType);
        Assert.Equal(1, applications?.TotalCount);
        Assert.Equal("iam-administration", Assert.Single(applications!.Items).ApplicationCode);
        Assert.Equal(1, users?.TotalCount);
        Assert.Equal("ADMIN001", Assert.Single(users!.Items).EmployeeCode);
        Assert.Equal("identity-admin-web", Assert.Single(catalog!.Clients).ClientId);
        Assert.Equal("iam.admin", Assert.Single(catalog.Capabilities).CapabilityCode);
        Assert.DoesNotContain("secretHash", catalogJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("iam.admin", Assert.Single(userAccess!.Applications).EffectiveCapabilities);
        Assert.Equal("Deny", Assert.Single(userAccess.Overrides).Effect);
        Assert.Equal("Identity Administrator", Assert.Single(applicationAccess!.Roles).RoleName);
        Assert.Equal("Password", Assert.Single(security!.Credentials).CredentialType);
        Assert.True(Assert.Single(security.Devices).IsTrusted);
        Assert.True(Assert.Single(security.MfaMethods).IsVerified);
        Assert.DoesNotContain("hash", securityJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", securityJson, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<GetAdministrationUserSecurityQuery>(factory.Dispatcher.LastRequest);
    }

    [Fact]
    public async Task UnexpectedErrors_ReturnStableProblemWithoutInternalDetails()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(
                "throw-user",
                "Secret-Horse-Battery-Staple-42",
                "client",
                null,
                null),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("server_error", body?.Code);
        Assert.DoesNotContain("Sensitive database detail", text, StringComparison.Ordinal);
        Assert.True(Guid.TryParse(body?.CorrelationId, out _));
    }

    [Fact]
    public async Task CorrelationHeader_IsPreservedInResponse()
    {
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", correlationId.ToString("D"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            correlationId.ToString("D"),
            Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task AuthenticationEndpoints_EnforceConfiguredRateLimit()
    {
        await using var limitedFactory = new IdentityApiFactory(authenticationPermitLimit: 2);
        using var client = limitedFactory.CreateClient();
        var request = new LoginRequest(
            "unknown-user",
            "Secret-Horse-Battery-Staple-42",
            "client",
            null,
            null);

        using var first = await client.PostAsJsonAsync(
            "/api/v1/auth/login", request, TestContext.Current.CancellationToken);
        using var second = await client.PostAsJsonAsync(
            "/api/v1/auth/login", request, TestContext.Current.CancellationToken);
        using var limited = await client.PostAsJsonAsync(
            "/api/v1/auth/login", request, TestContext.Current.CancellationToken);
        var body = await limited.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limit_exceeded", body?.Code);
    }

    [Fact]
    public async Task AuthenticationRateLimit_PartitionsByForwardedAddressFromTrustedProxy()
    {
        await using var limitedFactory = new IdentityApiFactory(
            authenticationPermitLimit: 1, reverseProxyAddress: "10.0.0.1");
        using var client = limitedFactory.CreateClient();
        var login = new LoginRequest(
            "unknown-user", "Secret-Horse-Battery-Staple-42", "client", null, null);

        using var firstClient = await LoginFrom(client, login, "198.51.100.10");
        using var secondClient = await LoginFrom(client, login, "198.51.100.11");
        using var repeatedFirstClient = await LoginFrom(client, login, "198.51.100.10");

        Assert.Equal(HttpStatusCode.Unauthorized, firstClient.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, repeatedFirstClient.StatusCode);
    }

    [Fact]
    public async Task OversizedRequest_IsRejectedBeforeModelBinding()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            new string('x', 70 * 1024),
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/api/v1/auth/login",
            content,
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("request_too_large", body?.Code);
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static async Task<HttpResponseMessage> LoginFrom(
        HttpClient client, LoginRequest login, string clientAddress)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(login),
        };
        request.Headers.Add("X-Forwarded-For", clientAddress);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage BrowserPost<T>(
        string path,
        T body,
        string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Identity-Session", "browser");
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.Add("Cookie", cookie);
        }

        return request;
    }
}
