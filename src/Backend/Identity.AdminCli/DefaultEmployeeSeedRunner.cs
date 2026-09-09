using System.Text.Json;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.AdminCli;

public sealed record DefaultEmployeeSeedResult(
    int Total, int Created, int Existing, int WouldCreate, bool Applied,
    IReadOnlyList<string> ExistingNameDifferences, Guid CorrelationId);

public sealed class DefaultEmployeeSeedRunner(
    IdentityDbContext db,
    IRequestDispatcher dispatcher,
    IAdministrationAuthorizer authorizer,
    ITransactionRunner transactions,
    TimeProvider timeProvider)
{
    public Task<DefaultEmployeeSeedResult> Run(
        IReadOnlyList<DefaultEmployee> roster, SeedDefaultEmployeesOptions options,
        CancellationToken cancellationToken)
    {
        DefaultEmployeeRoster.Validate(roster);
        if (options.ActorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        return transactions.Execute(async token =>
        {
            var context = new AdministrationContext(options.ActorUserId, Guid.NewGuid());
            await authorizer.Authorize(context, AdministrationAction.CreateUser, token);
            // Serialize seed replays. Concurrent ordinary user creation is also protected by the
            // normalized-code unique index; a collision rolls back the whole seed for safe retry.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'Identity.DefaultEmployeeSeed',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51009, 'Default employee seed is busy. Retry later.', 1;
                """, token);
            var codes = roster.Select(row => row.PersonnelNumber).ToArray();
            var existing = (await db.UserAccounts.AsNoTracking()
                .Where(user => codes.Contains(EF.Property<string>(user, "NormalizedEmployeeCode")))
                .Select(user => new { user.EmployeeCode, user.DisplayName }).ToListAsync(token))
                .ToDictionary(user => user.EmployeeCode.Trim(), StringComparer.OrdinalIgnoreCase);
            var missing = roster.Where(row => !existing.ContainsKey(row.PersonnelNumber)).ToArray();
            var differences = roster.Where(row => existing.TryGetValue(row.PersonnelNumber, out var user)
                    && user.DisplayName != row.Name)
                .Select(row => row.PersonnelNumber).ToArray();
            if (options.Apply)
            {
                foreach (var row in missing)
                    await dispatcher.Send(new CreateUserCommand(row.PersonnelNumber, row.Name, context, row.Email), token);
                if (missing.Length > 0)
                {
                    db.AuthenticationAudits.Add(AuthenticationAudit.CreateAdministrationEvent(
                        AdministrationAuditEventType.BulkImportCommitted, context.CorrelationId,
                        timeProvider.GetUtcNow().UtcDateTime, userId: options.ActorUserId,
                        eventDataJson: JsonSerializer.Serialize(new
                        {
                            source = "DefaultEmployeeSeed",
                            entityKey = "users",
                            actorUserId = options.ActorUserId,
                            created = missing.Length,
                            existing = existing.Count,
                            updated = 0,
                        })));
                    await db.SaveChangesAsync(token);
                }
            }
            return new DefaultEmployeeSeedResult(roster.Count, options.Apply ? missing.Length : 0,
                existing.Count, missing.Length, options.Apply, differences, context.CorrelationId);
        }, cancellationToken);
    }
}
