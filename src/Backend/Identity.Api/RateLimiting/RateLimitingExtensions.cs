using System.Globalization;
using System.Threading.RateLimiting;
using Identity.Api.Hosting;
using Identity.Contracts.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.RateLimiting;

internal static class RateLimitingExtensions
{
    public const string AuthenticationPolicy = "authentication";
    public const string SessionRefreshPolicy = "session-refresh";
    public const string SessionLogoutPolicy = "session-logout";
    public const string MachinePingPolicy = "machine-ping";
    public const string TerminalEmployeeSearchPolicy = "terminal-employee-search";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddIdentityRateLimiting(
        this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var httpContext = context.HttpContext;
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                ? delay
                : Window;
            httpContext.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
            httpContext.Response.Headers.CacheControl = "no-store";
            var response = new ApiErrorResponse(
                "https://identity.local/errors/rate_limit_exceeded",
                "Too many requests",
                StatusCodes.Status429TooManyRequests,
                "rate_limit_exceeded",
                RequestContextFactory.CorrelationId(httpContext).ToString("D"));
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        };
        AddPolicy(options, AuthenticationPolicy, settings => settings.AuthenticationPermitLimit);
        AddPolicy(options, SessionRefreshPolicy, settings => settings.SessionRefreshPermitLimit);
        AddPolicy(options, SessionLogoutPolicy, settings => settings.SessionLogoutPermitLimit);
        AddPolicy(options, MachinePingPolicy, _ => 12);
        AddPolicy(options, TerminalEmployeeSearchPolicy, _ => 30);
    });

    private static void AddPolicy(
        RateLimiterOptions options,
        string policy,
        Func<IdentityHostSettings, int> permitLimit) => options.AddPolicy(policy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Browser and non-browser routes share a budget within each policy, not across policies.
            $"{policy}:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit(context.RequestServices.GetRequiredService<IdentityHostSettings>()),
                Window = Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
}
