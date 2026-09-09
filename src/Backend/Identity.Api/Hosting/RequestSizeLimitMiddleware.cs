using Identity.Contracts.Errors;

namespace Identity.Api.Hosting;

internal sealed class RequestSizeLimitMiddleware(RequestDelegate next)
{
    private const long MaximumRequestBodyBytes = 64 * 1024;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var limit = context.Request.Path == "/api/v1/agents/report" ? 512 * 1024 : MaximumRequestBodyBytes;
        var feature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;
        if (context.Request.ContentLength > limit)
        {
            var response = new ApiErrorResponse(
                "https://identity.local/errors/request_too_large",
                "The request body is too large",
                StatusCodes.Status413PayloadTooLarge,
                "request_too_large",
                RequestContextFactory.CorrelationId(context).ToString("D"));
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(response);
            return;
        }

        await next(context);
    }
}
