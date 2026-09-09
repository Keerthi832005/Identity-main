using System.Security.Cryptography;

namespace Identity.Api.Hosting;

internal sealed class HostSettingsValidationService(
    IdentityHostSettings settings,
    RSA validationKey) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = settings.ConnectionString;
        _ = validationKey.KeySize;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
