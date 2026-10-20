using Identity.Application.Administration;

namespace Identity.AdminCli;

/// <summary>A transaction confined to the trusted CLI's first administrator bootstrap.</summary>
public interface IBootstrapSession
{
    Task<BootstrapResult> Execute(
        Func<AdministrationContext, CancellationToken, Task<BootstrapResult>> operation,
        CancellationToken cancellationToken);

    Task<AdministrationResult> CreateAdministrator(
        BootstrapOptions options,
        AdministrationContext context,
        CancellationToken cancellationToken);
}
