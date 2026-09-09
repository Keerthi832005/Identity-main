namespace Identity.Application.Administration;

public interface IAdministrationAuthorizer
{
    ValueTask Authorize(
        AdministrationContext context,
        AdministrationAction action,
        CancellationToken cancellationToken);
}
