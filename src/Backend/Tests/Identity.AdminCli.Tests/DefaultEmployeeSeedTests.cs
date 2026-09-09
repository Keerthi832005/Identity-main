using System.Text.Json;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.AdminCli.Tests;

public sealed class DefaultEmployeeSeedTests
{
    [Fact]
    public void EmbeddedRoster_PreservesPersonnelNumbersAndRepeatedNames()
    {
        var rows = DefaultEmployeeRoster.Load();
        Assert.Equal(164, rows.Count);
        Assert.Equal(164, rows.Select(row => row.PersonnelNumber).Distinct().Count());
        Assert.Equal(67, rows.Count(row => row.PersonnelNumber.StartsWith("INDC", StringComparison.Ordinal)));
        Assert.Contains(new DefaultEmployee("INDE00000", "Outsource"), rows);
        Assert.Contains(new DefaultEmployee("INDE04166", "Monika E"), rows);
        Assert.Equal(["INDE02131", "INDE03275"], rows.Where(row => row.Name == "Siddeswaran S")
            .Select(row => row.PersonnelNumber).ToArray());
        Assert.Equal("Siddeswaran.S@fujitec.co.in", rows.Single(row => row.PersonnelNumber == "INDE03275").Email);
        Assert.Null(rows.Single(row => row.PersonnelNumber == "INDE02131").Email);
        Assert.Throws<ArgumentException>(() => DefaultEmployeeRoster.Validate([rows[0], rows[0]]));
        Assert.Throws<ArgumentException>(() => DefaultEmployeeRoster.Validate([new("INDC00001", " ")]));
        Assert.Throws<ArgumentException>(() => DefaultEmployeeRoster.Validate([new("INDC00001", "Name", "not-an-email")]));
    }

    [Fact]
    public void Options_PreviewByDefaultAndRequireExplicitApply()
    {
        Assert.Equal(new(42, false), SeedDefaultEmployeesOptions.Parse(["--actor-user-id", "42"]));
        Assert.Equal(new(42, true), SeedDefaultEmployeesOptions.Parse(["--apply", "--actor-user-id", "42"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("--apply")]
    [InlineData("--actor-user-id 0")]
    [InlineData("--actor-user-id -1")]
    [InlineData("--actor-user-id 1 --apply --apply")]
    [InlineData("--actor-user-id 1 --actor-user-id 2")]
    [InlineData("--actor-user-id 1 --password default")]
    public void Options_RejectUnsafeOrAmbiguousArguments(string args) =>
        Assert.Throws<ArgumentException>(() => SeedDefaultEmployeesOptions.Parse(
            args.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

    [Fact]
    public async Task SqlSeed_IsAtomicAuditedAuthorizedRepeatableAndPreservesExistingAccounts()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Requires isolated SQL.");
        Assert.StartsWith("FIN_IAM_OrgMgmtTests_", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        var token = TestContext.Current.CancellationToken;
        var applicationCode = "seed-test-" + Guid.NewGuid().ToString("N");
        var setup = new ServiceCollection();
        setup.AddIdentityApplication();
        setup.AddIdentityPersistence(connection);
        setup.AddSingleton<IAdministrationAuthorizer, AllowSetup>();
        await using var setupProvider = setup.BuildServiceProvider();
        await using var setupScope = setupProvider.CreateAsyncScope();
        var commands = setupScope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var actor = await commands.Send(new CreateUserCommand(applicationCode, "Seed test administrator", context), token);
        context = context with { ActorUserId = actor.ResourceId };
        var app = await commands.Send(new CreateApplicationCommand(applicationCode, "Seed test", null, applicationCode, 15, 7, context), token);
        var module = await commands.Send(new CreateModuleCommand(app.ResourceId, "admin", "Admin", null, null, 0, true, context), token);
        var capability = await commands.Send(new CreateCapabilityCommand(app.ResourceId, module.ResourceId, "iam.admin", "Admin", null, context), token);
        var role = await commands.Send(new CreateRoleCommand(app.ResourceId, "admin", "Admin", null, true, context), token);
        await commands.Send(new GrantUserApplicationCommand(actor.ResourceId, app.ResourceId, context), token);
        await commands.Send(new AssignRoleCommand(actor.ResourceId, app.ResourceId, role.ResourceId, context), token);
        await commands.Send(new GrantRolePermissionCommand(app.ResourceId, role.ResourceId, capability.ResourceId, context), token);
        var preserved = await commands.Send(new CreateUserCommand("indc00001", "Existing name", context, "keep@example.test"), token);
        await commands.Send(new GrantUserApplicationCommand(preserved.ResourceId, app.ResourceId, context), token);
        var setupDb = setupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var oldUser = await setupDb.UserAccounts.SingleAsync(user => user.UserId == preserved.ResourceId, token);
        oldUser.SetActive(false, DateTime.UtcNow);
        (await setupDb.UserCredentials.SingleAsync(row => row.UserId == preserved.ResourceId && row.RevokedAt == null, token)).Revoke(DateTime.UtcNow);
        var pin = UserCredential.CreatePin(oldUser.UserId, "PBKDF2-SHA256", 100000, new byte[32], new byte[32], DateTime.UtcNow, actor.ResourceId);
        setupDb.UserCredentials.Add(pin);
        await setupDb.SaveChangesAsync(token);
        setupDb.ChangeTracker.Clear();
        var preservedJson = JsonSerializer.Serialize(await setupDb.UserAccounts.AsNoTracking().SingleAsync(user => user.UserId == preserved.ResourceId, token));
        var pinJson = JsonSerializer.Serialize(await setupDb.UserCredentials.AsNoTracking().SingleAsync(row => row.UserId == preserved.ResourceId && row.RevokedAt == null, token));
        var grants = await setupDb.UserApplications.CountAsync(token);

        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        services.AddSingleton(new ProvisioningActor(actor.ResourceId, applicationCode, "iam.admin"));
        services.AddScoped<IAdministrationAuthorizer, ProvisioningAdministrationAuthorizer>();
        services.AddScoped<DefaultEmployeeSeedRunner>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<DefaultEmployeeSeedRunner>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var roster = DefaultEmployeeRoster.Load();
        var before = await db.UserAccounts.CountAsync(token);
        var preview = await runner.Run(roster, new(actor.ResourceId, false), token);
        Assert.Equal(163, preview.WouldCreate);
        Assert.Equal(0, preview.Created);
        Assert.Equal(before, await db.UserAccounts.CountAsync(token));
        var applied = await runner.Run(roster, new(actor.ResourceId, true), token);
        Assert.Equal(163, applied.Created);
        Assert.Equal("Siddeswaran.S@fujitec.co.in", (await db.UserAccounts.AsNoTracking()
            .SingleAsync(row => row.EmployeeCode == "INDE03275", token)).Email);
        Assert.Equal(1, applied.Existing);
        Assert.Equal(["INDC00001"], applied.ExistingNameDifferences);
        Assert.Equal(163, await db.AuthenticationAudits.CountAsync(row => row.CorrelationId == applied.CorrelationId && row.EventType == "UserCreated", token));
        Assert.Equal(1, await db.AuthenticationAudits.CountAsync(row => row.CorrelationId == applied.CorrelationId && row.EventType == "BulkImportCommitted" && row.UserId == actor.ResourceId, token));
        var audits = await db.AuthenticationAudits.CountAsync(token);
        var replay = await runner.Run(roster, new(actor.ResourceId, true), token);
        Assert.Equal(0, replay.Created);
        Assert.Equal(164, replay.Existing);
        Assert.Equal(audits, await db.AuthenticationAudits.CountAsync(token));
        Assert.Equal(preservedJson, JsonSerializer.Serialize(await db.UserAccounts.AsNoTracking().SingleAsync(user => user.UserId == preserved.ResourceId, token)));
        Assert.Equal(pinJson, JsonSerializer.Serialize(await db.UserCredentials.AsNoTracking().SingleAsync(row => row.UserId == preserved.ResourceId && row.RevokedAt == null, token)));
        Assert.Equal(grants, await db.UserApplications.CountAsync(token));
        Assert.Equal(164, await db.UserCredentials.CountAsync(row => row.UserId != actor.ResourceId && row.RevokedAt == null, token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runner.Run(roster, new(preserved.ResourceId, true), token));

        var failRunner = new DefaultEmployeeSeedRunner(db,
            new FailSecondCreation(scope.ServiceProvider.GetRequiredService<IRequestDispatcher>()),
            scope.ServiceProvider.GetRequiredService<IAdministrationAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<ITransactionRunner>(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failRunner.Run(
            [new("INDE09997", "Rollback one"), new("INDE09998", "Rollback two")], new(actor.ResourceId, true), token));
        Assert.False(await db.UserAccounts.AnyAsync(user => user.EmployeeCode == "INDE09997", token));
        Assert.Equal(audits, await db.AuthenticationAudits.CountAsync(token));
        // Revoking the actor's effective permission is rejected even when the actor ID matches.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Identity].[UserAccount] SET IsActive=0 WHERE UserId={actor.ResourceId}", token);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runner.Run(roster, new(actor.ResourceId, true), token));
    }

    private sealed class AllowSetup : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FailSecondCreation(IRequestDispatcher inner) : IRequestDispatcher
    {
        private int count;
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (++count == 2) throw new InvalidOperationException("Synthetic seed failure");
            return inner.Send(request, cancellationToken);
        }
    }
}
