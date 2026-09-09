using Identity.Api.Hosting;
using Identity.Api.RateLimiting;
using Identity.Api.Validation;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Contracts.Authentication;

namespace Identity.Api.Endpoints;

internal static class AuthenticationEndpoints
{
    private const string BrowserSessionHeader = "X-Identity-Session";
    private const string BrowserSessionHeaderValue = "browser";

    public static IEndpointRouteBuilder MapIdentityAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .AllowAnonymous();
        group.MapPost("/login", Login)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<LoginRequest>>();
        group.MapPost("/mfa/complete", CompleteMfa)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<CompleteMfaRequest>>();
        group.MapPost("/refresh", Refresh)
            .RequireRateLimiting(RateLimitingExtensions.SessionRefreshPolicy)
            .AddEndpointFilter<RequestValidationFilter<RefreshTokenRequest>>();
        group.MapPost("/logout", Logout)
            .RequireRateLimiting(RateLimitingExtensions.SessionLogoutPolicy)
            .AddEndpointFilter<RequestValidationFilter<LogoutRequest>>();
        group.MapPost("/terminal/verify", VerifyTerminal)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<TerminalVerificationRequest>>();
        group.MapPost("/terminal/current", VerifyCurrentTerminal)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy);
        group.MapPost("/terminal/users", GetTerminalUsers)
            .RequireRateLimiting(RateLimitingExtensions.TerminalEmployeeSearchPolicy);
        var browser = group.MapGroup("/browser");
        browser.MapPost("/terminal-employees/search", SearchTerminalEmployees)
            .RequireRateLimiting(RateLimitingExtensions.TerminalEmployeeSearchPolicy);
        browser.MapPost("/login", BrowserLogin)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<LoginRequest>>();
        browser.MapPost("/mfa/complete", BrowserCompleteMfa)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<CompleteMfaRequest>>();
        browser.MapPost("/refresh", BrowserRefresh)
            .RequireRateLimiting(RateLimitingExtensions.SessionRefreshPolicy)
            .AddEndpointFilter<RequestValidationFilter<BrowserRefreshRequest>>();
        browser.MapPost("/password", BrowserChangePassword)
            .RequireRateLimiting(RateLimitingExtensions.AuthenticationPolicy)
            .AddEndpointFilter<RequestValidationFilter<BrowserChangePasswordRequest>>();
        browser.MapPost("/logout", BrowserLogout)
            .RequireRateLimiting(RateLimitingExtensions.SessionLogoutPolicy);
        return endpoints;
    }

    private static async Task<IResult> BrowserChangePassword(
        BrowserChangePasswordRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        IdentityHostSettings settings,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IsBrowserSessionRequest(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!context.Request.Cookies.TryGetValue(
                settings.BrowserSessionCookieName,
                out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteBrowserSessionCookie(context, settings);
            return Results.Unauthorized();
        }

        var result = await dispatcher.Send(new ChangeOwnPasswordCommand(
            refreshToken,
            request.ClientId,
            request.CurrentPassword,
            request.NewPassword,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        if (result.Succeeded)
        {
            DeleteBrowserSessionCookie(context, settings);
            return Results.NoContent();
        }

        if (result.FailureCode == AuthenticationFailureCode.InvalidCredentials)
        {
            return Results.BadRequest(new
            {
                detail = "Your current password is incorrect.",
                failureCode = result.FailureCode.ToString(),
            });
        }

        DeleteBrowserSessionCookie(context, settings);
        return Results.Unauthorized();
    }

    private sealed record CurrentTerminalRequest(
        long TerminalId,
        string ClientId,
        string ClientSecret,
        string? NewPin = null);

    private static async Task<IResult> VerifyCurrentTerminal(
        CurrentTerminalRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (request.TerminalId <= 0 || string.IsNullOrWhiteSpace(request.ClientId)
            || request.ClientId.Length > 150 || string.IsNullOrWhiteSpace(request.ClientSecret)
            || request.ClientSecret.Length > 4096
            || !System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter) || header.Parameter.Length > 32768)
            return Results.Unauthorized();
        if (request.NewPin is { } pin && (pin.Length != 4 || pin.Any(c => c is < '0' or > '9')))
            return Results.BadRequest(new { failureCode = "InvalidNewPin" });
        var result = await dispatcher.Send(new CurrentTerminalVerificationCommand(
            request.TerminalId, header.Parameter, request.ClientId, request.ClientSecret,
            RequestContextFactory.CorrelationId(context), request.NewPin), cancellationToken);
        var response = new TerminalVerificationResponse(result.Succeeded, result.FailureCode?.ToString(),
            result.UserId, result.EmployeeCode, result.DisplayName, result.CapabilityCodes);
        return Results.Json(response, statusCode: result.Succeeded ? StatusCodes.Status200OK
            : result.FailureCode is TerminalVerificationFailureCode.PinChangeRequired or TerminalVerificationFailureCode.InvalidNewPin
                ? StatusCodes.Status409Conflict
            : result.FailureCode is TerminalVerificationFailureCode.AccessDenied or TerminalVerificationFailureCode.TerminalNotTrusted
                ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized);
    }

    private sealed record TerminalEmployeeSearchRequest(string? ClientId, string? Search);

    private sealed record TerminalUserDirectoryRequest(
        long TerminalId,
        string? ClientId,
        string? ClientSecret,
        string? Search,
        int Skip = 0,
        int Take = 100);

    private static async Task<IResult> GetTerminalUsers(
        TerminalUserDirectoryRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await dispatcher.Send(new GetTerminalUserDirectoryQuery(
            request.TerminalId,
            request.ClientId ?? "",
            request.ClientSecret ?? "",
            request.Search,
            request.Skip,
            request.Take), cancellationToken);
        if (result.Succeeded) return Results.Ok(result.Page);
        return Results.Json(new { succeeded = false, failureCode = result.FailureCode?.ToString() },
            statusCode: result.FailureCode == TerminalVerificationFailureCode.TerminalNotTrusted
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> SearchTerminalEmployees(
        TerminalEmployeeSearchRequest request,
        HttpContext context,
        ITerminalEmployeeDirectory directory,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IsBrowserSessionRequest(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var search = request.Search?.Trim() ?? "";
        var clientId = request.ClientId?.Trim() ?? "";
        if (search.Length is < 3 or > 50 || clientId.Length is < 1 or > 150)
            return Results.Ok(Array.Empty<TerminalEmployeeSuggestion>());
        return Results.Ok(await directory.Search(clientId, search, cancellationToken));
    }

    private static async Task<IResult> BrowserLogin(
        LoginRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        IdentityHostSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsBrowserSessionRequest(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (request.ClientSecret is not null)
        {
            throw new ArgumentException(
                "Browser login cannot accept a client secret.",
                nameof(request));
        }

        var result = await dispatcher.Send(new LoginCommand(
            request.EmployeeCode,
            request.Password,
            request.ClientId,
            request.ClientSecret,
            request.DeviceId,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return MapBrowser(result, context, settings);
    }

    private static async Task<IResult> BrowserCompleteMfa(
        CompleteMfaRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        IdentityHostSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsBrowserSessionRequest(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (request.MfaChallengeId == Guid.Empty)
        {
            throw new ArgumentException("MFA challenge id is required.", nameof(request));
        }

        var result = await dispatcher.Send(new CompleteMfaLoginCommand(
            request.MfaChallengeId,
            request.Code,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return MapBrowser(result, context, settings);
    }

    private static async Task<IResult> BrowserRefresh(
        BrowserRefreshRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        IdentityHostSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsBrowserSessionRequest(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!context.Request.Cookies.TryGetValue(
            settings.BrowserSessionCookieName,
            out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteBrowserSessionCookie(context, settings);
            return Results.Unauthorized();
        }

        var result = await dispatcher.Send(new RefreshSessionCommand(
            refreshToken,
            request.ClientId,
            null,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        if (!result.Succeeded)
        {
            DeleteBrowserSessionCookie(context, settings);
        }

        return MapBrowser(result, context, settings);
    }

    private static async Task<IResult> BrowserLogout(
        HttpContext context,
        IRequestDispatcher dispatcher,
        IdentityHostSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IsBrowserSessionRequest(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        OperationResult result;
        if (context.Request.Cookies.TryGetValue(
            settings.BrowserSessionCookieName,
            out var refreshToken)
            && !string.IsNullOrWhiteSpace(refreshToken))
        {
            result = await dispatcher.Send(new LogoutCommand(
                refreshToken,
                RequestContextFactory.CorrelationId(context)), cancellationToken);
        }
        else
        {
            result = OperationResult.Success;
        }

        DeleteBrowserSessionCookie(context, settings);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new LoginCommand(
            request.EmployeeCode,
            request.Password,
            request.ClientId,
            request.ClientSecret,
            request.DeviceId,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return Map(result);
    }

    private static async Task<IResult> CompleteMfa(
        CompleteMfaRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (request.MfaChallengeId == Guid.Empty)
        {
            throw new ArgumentException("MFA challenge id is required.", nameof(request));
        }

        var result = await dispatcher.Send(new CompleteMfaLoginCommand(
            request.MfaChallengeId,
            request.Code,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return Map(result);
    }

    private static async Task<IResult> Refresh(
        RefreshTokenRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new RefreshSessionCommand(
            request.RefreshToken,
            request.ClientId,
            request.ClientSecret,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return Map(result);
    }

    private static async Task<IResult> Logout(
        LogoutRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new LogoutCommand(
            request.RefreshToken,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> VerifyTerminal(
        TerminalVerificationRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new TerminalVerificationCommand(
            request.TerminalId,
            request.EmployeeCode,
            request.Pin,
            request.ClientId,
            request.ClientSecret,
            RequestContextFactory.CorrelationId(context)), cancellationToken);
        var response = new TerminalVerificationResponse(
            result.Succeeded,
            result.FailureCode?.ToString(),
            result.UserId,
            result.EmployeeCode,
            result.DisplayName,
            result.CapabilityCodes ?? []);
        if (result.Succeeded)
        {
            return Results.Ok(response);
        }

        var status = result.FailureCode is TerminalVerificationFailureCode.PinChangeRequired
            ? StatusCodes.Status409Conflict
            : result.FailureCode is TerminalVerificationFailureCode.AccessDenied
            or TerminalVerificationFailureCode.TerminalNotTrusted
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return Results.Json(response, statusCode: status);
    }

    private static IResult Map(AuthenticationResult result)
    {
        var response = new AuthenticationResponse(
            result.Succeeded,
            result.FailureCode?.ToString(),
            result.AccessToken,
            result.AccessTokenExpiresAt,
            result.RefreshToken,
            result.RefreshTokenExpiresAt,
            result.AuthorizationVersion,
            result.MfaChallengeId);
        if (result.Succeeded)
        {
            return Results.Ok(response);
        }

        if (result.MfaChallengeId.HasValue)
        {
            return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
        }

        var status = result.FailureCode == AuthenticationFailureCode.AccessDenied
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return Results.Json(response, statusCode: status);
    }

    private static IResult MapBrowser(
        AuthenticationResult result,
        HttpContext context,
        IdentityHostSettings settings)
    {
        if (result.Succeeded
            && result.RefreshToken is { Length: > 0 } refreshToken
            && result.RefreshTokenExpiresAt.HasValue)
        {
            context.Response.Cookies.Append(
                settings.BrowserSessionCookieName,
                refreshToken,
                BrowserCookieOptions(settings, result.RefreshTokenExpiresAt.Value));
        }

        var response = new AuthenticationResponse(
            result.Succeeded,
            result.FailureCode?.ToString(),
            result.AccessToken,
            result.AccessTokenExpiresAt,
            null,
            result.RefreshTokenExpiresAt,
            result.AuthorizationVersion,
            result.MfaChallengeId);
        if (result.Succeeded)
        {
            return Results.Ok(response);
        }

        if (result.MfaChallengeId.HasValue)
        {
            return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
        }

        var status = result.FailureCode == AuthenticationFailureCode.AccessDenied
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return Results.Json(response, statusCode: status);
    }

    private static bool IsBrowserSessionRequest(HttpContext context) =>
        context.Request.Headers.TryGetValue(BrowserSessionHeader, out var values)
        && values.Count == 1
        && string.Equals(values[0], BrowserSessionHeaderValue, StringComparison.Ordinal);

    private static CookieOptions BrowserCookieOptions(
        IdentityHostSettings settings,
        DateTime expiresAt) => new()
        {
            HttpOnly = true,
            Secure = settings.RequireSecureBrowserSessionCookie,
            SameSite = SameSiteMode.Strict,
            Path = settings.BrowserSessionCookiePath,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
            IsEssential = true,
        };

    private static void DeleteBrowserSessionCookie(
        HttpContext context,
        IdentityHostSettings settings) => context.Response.Cookies.Delete(
        settings.BrowserSessionCookieName,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = settings.RequireSecureBrowserSessionCookie,
            SameSite = SameSiteMode.Strict,
            Path = settings.BrowserSessionCookiePath,
            IsEssential = true,
        });
}
