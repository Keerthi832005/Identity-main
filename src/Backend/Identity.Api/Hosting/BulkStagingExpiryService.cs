using Identity.Application.BulkData;

namespace Identity.Api.Hosting;

/// <summary>
/// Removes abandoned staged batches once they pass their expiry.
///
/// Staged rows hold administrator-supplied identity data, so leaving them indefinitely would grow a
/// second copy of the directory outside the tables that own it. A committed or discarded batch is
/// already gone; this only reaches batches nobody came back to.
/// </summary>
internal sealed class BulkStagingExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<BulkStagingExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private const int BatchesPerSweep = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Sweep(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                /* A failed sweep must not stop the service: the next tick retries, and staged data
                   lingering one interval longer is better than never sweeping again. */
                logger.LogError(
                    exception,
                    "Bulk staging expiry sweep failed; retrying at the next interval.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task Sweep(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IBulkStagingStore>();
        var expired = await store
            .ListExpired(timeProvider.GetUtcNow().UtcDateTime, BatchesPerSweep, cancellationToken)
            .ConfigureAwait(false);

        foreach (var batch in expired)
        {
            await store.Remove(batch, cancellationToken).ConfigureAwait(false);
        }

        if (expired.Count > 0)
        {
            logger.LogInformation(
                "Removed {Count} expired bulk staging batches.",
                expired.Count);
        }
    }
}
