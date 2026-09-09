using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Identity.Infrastructure.Persistence.Interceptors;

public sealed class AppendOnlyAuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RejectAuditMutation(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RejectAuditMutation(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void RejectAuditMutation(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var mutation = context.ChangeTracker.Entries<AuthenticationAudit>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (mutation is not null)
        {
            throw new InvalidOperationException("Authentication audit is append-only.");
        }
    }
}
