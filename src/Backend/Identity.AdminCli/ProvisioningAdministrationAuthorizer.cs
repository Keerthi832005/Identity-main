using Identity.Application.Administration;

namespace Identity.AdminCli;

internal sealed class ProvisioningAdministrationAuthorizer(
    IAdministrationStore store,
    ProvisioningActor actor,
    TimeProvider timeProvider) : IAdministrationAuthorizer
{
    public async ValueTask Authorize(
        AdministrationContext context,
        AdministrationAction action,
        CancellationToken cancellationToken)
    {
        if (context.ActorUserId != actor.UserId)
        {
            throw new UnauthorizedAccessException(
                $"Provisioning action '{action}' has an invalid actor.");
        }

        var application = await store.FindApplicationByCode(
            actor.AdministrationApplicationCode,
            cancellationToken);
        var authorization = application is null
            ? null
            : await store.GetEffectiveAuthorization(
                actor.UserId,
                application.ApplicationId,
                timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);
        if (authorization?.CapabilityCodes.Contains(
            actor.RequiredCapability,
            StringComparer.OrdinalIgnoreCase) != true)
        {
            throw new UnauthorizedAccessException(
                $"Provisioning action '{action}' requires '{actor.RequiredCapability}'.");
        }
    }
}
