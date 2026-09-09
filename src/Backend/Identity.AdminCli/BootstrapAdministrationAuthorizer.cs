using Identity.Application.Administration;

namespace Identity.AdminCli;

internal sealed class BootstrapAdministrationAuthorizer : IAdministrationAuthorizer
{
    public ValueTask Authorize(
        AdministrationContext context,
        AdministrationAction action,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
