using System.Globalization;
using Identity.Application.Administration;

namespace Identity.Api.Hosting;

internal static class RequestContextFactory
{
    public static Guid CorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemName, out var value)
        && value is Guid correlationId
            ? correlationId
            : throw new InvalidOperationException("Request correlation id is unavailable.");

    public static AdministrationContext Administration(HttpContext context)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (!long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var actorUserId)
            || actorUserId <= 0)
        {
            throw new UnauthorizedAccessException("A valid administration subject is required.");
        }

        return new AdministrationContext(actorUserId, CorrelationId(context));
    }
}
