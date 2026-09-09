using Identity.Application.Administration;
using Identity.Application.Persistence;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.AdminCli;

public sealed record DefaultAdministratorSeedResult(
    string EmployeeCode, string DisplayName, string Email, string Role,
    bool Applied, bool Created, long? UserId, string State);

/// <summary>Explicit initial administrator seed; never resets credentials or restores revoked access.</summary>
public sealed class DefaultAdministratorSeedRunner(
    IdentityDbContext db,
    IAdministrationStore store,
    ITransactionRunner transactions,
    AdministrationWebProvisioningRunner web,
    TimeProvider clock)
{
    public static BootstrapOptions Profile
    {
        get
        {
            var employee = DefaultEmployeeRoster.Load().Single(row => row.PersonnelNumber == "INDE03275");
            return new(employee.PersonnelNumber, employee.Name, "iam-administration",
                "Identity Administration", "identity-bootstrap-client", "urn:identity:administration",
                employee.Email ?? throw new InvalidOperationException("Default administrator email is missing."));
        }
    }

    public Task<DefaultAdministratorSeedResult> Run(
        bool apply, Func<CancellationToken, Task<BootstrapResult>> bootstrap, CancellationToken cancellationToken)
    {
        var profile = Profile;
        return transactions.Execute(async token =>
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource=N'Identity.DefaultAdministratorSeed',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51009, 'Administrator seed is busy. Retry later.', 1;
                """, token);
            var user = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(
                row => EF.Property<string>(row, "NormalizedEmployeeCode") == profile.EmployeeCode, token);
            if (user is not null)
            {
                if (!user.IsActive || !string.Equals(user.Email, profile.Email, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The existing administrator is inactive or has a different email. Review the account; it was not changed.");
                await RequireAdministrator(user.UserId, token);
                return Result(apply, false, user.UserId, "already-seeded");
            }

            // Use the established secure bootstrap only on an empty, migrated IAM database.
            if (await db.UserAccounts.AnyAsync(token) || await db.Applications.AnyAsync(token))
                throw new InvalidOperationException("Initial administrator seeding requires an empty IAM database. Use authorized administration for an existing installation.");
            if (!apply) return Result(false, false, null, "would-create");

            var created = await bootstrap(token);
            await web.Run(new(profile.EmployeeCode), token);
            await RequireAdministrator(created.AdministratorUserId, token);
            return Result(true, true, created.AdministratorUserId, "created");
        }, cancellationToken);

        DefaultAdministratorSeedResult Result(bool applied, bool created, long? id, string state) =>
            new(profile.EmployeeCode, profile.DisplayName, profile.Email!, "identity-administrator", applied, created, id, state);
    }

    private async Task RequireAdministrator(long userId, CancellationToken token)
    {
        var application = await store.FindApplicationByCode("iam-administration", token);
        var access = application is null ? null : await store.GetEffectiveAuthorization(
            userId, application.ApplicationId, clock.GetUtcNow().UtcDateTime, token);
        if (access?.CapabilityCodes.Contains("iam.admin", StringComparer.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("The existing user does not have active IAM administrator access. The seed does not restore revoked permissions.");
    }
}
