using Identity.Api.Hosting;
using Identity.Contracts.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Identity.Api.Errors;

internal static class BearerEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            await Write(context.HttpContext, 401, "Authentication is required", "authentication_required");
        },
        OnForbidden = context => Write(
            context.HttpContext,
            403,
            "The required capability is missing",
            "insufficient_capability"),
    };

    private static Task Write(HttpContext context, int status, string title, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var response = new ApiErrorResponse(
            $"https://identity.local/errors/{code}",
            title,
            status,
            code,
            RequestContextFactory.CorrelationId(context).ToString("D"));
        return context.Response.WriteAsJsonAsync(response);
    }
}
