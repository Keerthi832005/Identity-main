using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.AdminCli;

/// <summary>Creates the first administrator before organization masters exist.</summary>
internal sealed class BootstrapSession(
    IdentityDbContext db,
    IAdministrationStore store,
    ITransactionRunner transactions,
    IPinHasher pinHasher,
    TimeProvider clock) : IBootstrapSession
{
    private AdministrationContext? activeContext;

    public Task<BootstrapResult> Execute(
        Func<AdministrationContext, CancellationToken, Task<BootstrapResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (activeContext is not null) throw new InvalidOperationException("Bootstrap is already active.");
        return transactions.Execute(async token =>
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource=N'Identity.FirstAdministratorBootstrap',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51009, 'Administrator bootstrap is busy. Retry later.', 1;
                """, token);
            if (await db.UserAccounts.AnyAsync(token) || await db.Applications.AnyAsync(token))
                throw new InvalidOperationException("Bootstrap requires an empty migrated Identity database.");
            activeContext = new AdministrationContext(null, Guid.NewGuid());
            try { return await operation(activeContext, token); }
            finally { activeContext = null; }
        }, cancellationToken);
    }

    public async Task<AdministrationResult> CreateAdministrator(
        BootstrapOptions options,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        if (activeContext is null || context != activeContext || context.ActorUserId is not null
            || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Initial administrator creation requires the active trusted bootstrap transaction.");
        if (await db.UserAccounts.AnyAsync(cancellationToken))
            throw new InvalidOperationException("The initial administrator already exists.");
        var application = await db.Applications.SingleAsync(cancellationToken);
        if (application.ApplicationCode != options.ApplicationCode || application.TokenAudience != options.Audience
            || !application.IsActive)
            throw new InvalidOperationException("The bootstrap application does not match the approved options.");
        var now = clock.GetUtcNow().UtcDateTime;
        var user = UserAccount.Create(options.EmployeeCode, options.DisplayName, now, options.Email);
        store.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        if (InitialEmployeePin.FromEmployeeCode(user.EmployeeCode) is { } initialPin)
        {
            var hash = pinHasher.Hash(initialPin);
            store.Add(UserCredential.CreatePin(user.UserId, hash.Algorithm, hash.IterationCount,
                hash.Salt, hash.Hash, now, null, requiresChange: true));
        }
        store.Add(AuthenticationAudit.CreateAdministrationEvent(
            AdministrationAuditEventType.UserCreated, context.CorrelationId, now, userId: user.UserId));
        await db.SaveChangesAsync(cancellationToken);
        return new AdministrationResult("User", user.UserId, user.UserId, null, null);
    }
}
