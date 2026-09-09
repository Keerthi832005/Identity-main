using Identity.Infrastructure.Persistence;

namespace Identity.Api.Health;

internal sealed class DatabaseReadinessProbe(IdentityDbContext dbContext) : IReadinessProbe
{
    public Task<bool> IsReady(CancellationToken cancellationToken) =>
        dbContext.Database.CanConnectAsync(cancellationToken);
}
