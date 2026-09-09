namespace Identity.Api.Hosting;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "Identity.CorrelationId";

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            && Guid.TryParse(supplied, out var parsed)
            && parsed != Guid.Empty
                ? parsed
                : Guid.NewGuid();
        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString("D");
        await next(context);
    }
}
