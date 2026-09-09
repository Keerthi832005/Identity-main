using System.Globalization;
using Identity.Application.Administration;

namespace Identity.Api.Administration;

internal sealed class ClaimsAdministrationAuthorizer(IHttpContextAccessor accessor)
    : IAdministrationAuthorizer
{
    public ValueTask Authorize(
        AdministrationContext context,
        AdministrationAction action,
        CancellationToken cancellationToken)
    {
        var principal = accessor.HttpContext?.User;
        var subject = principal?.FindFirst("sub")?.Value;
        var actorMatches = context.ActorUserId.HasValue
            && long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var subjectId)
            && subjectId == context.ActorUserId.Value;
        var isAdministrator = principal?.Claims.Any(claim =>
            claim.Type == AdministrationPolicy.CapabilityClaim
            && claim.Value == AdministrationPolicy.RequiredCapability) == true;
        if (principal?.Identity?.IsAuthenticated != true || !actorMatches || !isAdministrator)
        {
            throw new UnauthorizedAccessException(
                $"Administration action '{action}' is not authorized.");
        }

        return ValueTask.CompletedTask;
    }
}
