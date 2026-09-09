using System.Data.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Identity.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    private static readonly string[] ExpectedTables =
    [
        "Identity.AgentInstallation",
        "Identity.AgentCollectState",
        "Identity.AgentControlState",
        "Identity.AgentUpdateRequest",
        "Identity.Application",
        "Identity.ApplicationClient",
        "Identity.ApplicationModule",
        "Identity.AuthenticationAudit",
        "Identity.Device",
        "Identity.MfaChallenge",
        "Identity.ModuleCapability",
        "Identity.RefreshToken",
        "Identity.Role",
        "Identity.RolePermission",
        "Identity.UserAccount",
        "Identity.UserApplication",
        "Identity.UserCredential",
        "Identity.UserMfaMethod",
        "Identity.UserPermissionOverride",
        "Identity.UserRole",
        "Identity.Organization",
        "Identity.OrganizationUnit",
        "Identity.Country",
        "Identity.Region",
        "Identity.State",
        "Identity.Branch",
        "Identity.Location",
        "Identity.Department",
        "Identity.Team",
        "Identity.BulkImportBatch",
        "Identity.BulkImportRow",
    ];

    [Fact]
    public void Model_MapsAllApprovedIdentityTables()
    {
        using var context = CreateContext(ModelOnlyConnectionString);

        var actual = context.Model.GetEntityTypes()
            .Select(type => $"{type.GetSchema()}.{type.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTables.Order(StringComparer.Ordinal), actual);
        Assert.DoesNotContain(context.Model.GetEntityTypes(), type => type.GetSchema() is null);
    }

    [Fact]
    public void Model_MapsComputedNormalizationColumns()
    {
        using var context = CreateContext(ModelOnlyConnectionString);
        var application = context.Model.FindEntityType(typeof(RegisteredApplication))
            ?? throw new InvalidOperationException("Application mapping is required.");

        var code = application.FindProperty("NormalizedApplicationCode");
        var audience = application.FindProperty("NormalizedTokenAudience");

        Assert.NotNull(code?.GetComputedColumnSql());
        Assert.NotNull(audience?.GetComputedColumnSql());
    }

    [Fact]
    public async Task AppendOnlyInterceptor_RejectsAuditModification()
    {
        var audit = (AuthenticationAudit)(Activator.CreateInstance(
            typeof(AuthenticationAudit), nonPublic: true)
            ?? throw new InvalidOperationException("Audit construction failed."));
        await using var context = CreateContext(ModelOnlyConnectionString, new AppendOnlyAuditInterceptor());
        var entry = context.Entry(audit);
        entry.Property(x => x.AuthenticationAuditId).CurrentValue = 1;
        entry.State = EntityState.Modified;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("append-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlServerSchema_ContainsEveryMappedTableAndColumn()
    {
        var connectionString = GetSqlTestConnectionString();

        await using var context = CreateContext(connectionString);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            await AssertMappedColumnsExist(
                context.Database.GetDbConnection(), entityType, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task SqlServerSchema_EnforcesNormalizedApplicationUniqueness()
    {
        var connectionString = GetSqlTestConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        var suffix = Guid.NewGuid().ToString("N");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Identity].[Application]
                ([ApplicationCode], [ApplicationName], [TokenAudience])
            VALUES
                ({$"app-{suffix}"}, {$"Application {suffix}"}, {$"urn:identity:{suffix}"});
            """,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Identity].[Application]
                    ([ApplicationCode], [ApplicationName], [TokenAudience])
                VALUES
                    ({$" APP-{suffix} "}, {$"Duplicate {suffix}"}, {$"urn:identity:duplicate:{suffix}"});
                """,
                TestContext.Current.CancellationToken));

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SqlServerSchema_AllowsOnlyValidClientSecretVersions()
    {
        var connectionString = GetSqlTestConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        var suffix = Guid.NewGuid().ToString("N");

        var applicationCode = $"client-version-{suffix}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Identity].[Application]
                ([ApplicationCode], [ApplicationName], [TokenAudience])
            VALUES
                ({applicationCode}, {$"Client Version {suffix}"}, {$"client-version-api-{suffix}"});
            """,
            TestContext.Current.CancellationToken);
        var applicationId = await context.Database.SqlQuery<long>(
            $"""
            SELECT [ApplicationId] AS [Value]
            FROM [Identity].[Application]
            WHERE [ApplicationCode] = {applicationCode}
            """).SingleAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Identity].[ApplicationClient]
                ([ApplicationId], [ClientId], [ClientName], [ClientType], [SecretVersion])
            VALUES
                ({applicationId}, {$"public-{suffix}"}, {"Public Client"}, {"Public"}, {0});
            """,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Identity].[ApplicationClient]
                    ([ApplicationId], [ClientId], [ClientName], [ClientType], [SecretVersion])
                VALUES
                    ({applicationId}, {$"invalid-public-{suffix}"}, {"Invalid Public"}, {"Public"}, {1});
                """,
                TestContext.Current.CancellationToken));

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SqlServerSchema_RejectsAuthenticationAuditUpdates()
    {
        var connectionString = GetSqlTestConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        var correlationId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Identity].[AuthenticationAudit]
                ([EventType], [Succeeded], [CorrelationId], [OccurredAt])
            VALUES
                ({"LoginSucceeded"}, {true}, {correlationId}, {occurredAt});
            """,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE [Identity].[AuthenticationAudit]
                SET [FailureCode] = {"ForbiddenMutation"}
                WHERE [CorrelationId] = {correlationId};
                """,
                TestContext.Current.CancellationToken));

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private const string ModelOnlyConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Identity_ModelOnly;Integrated Security=true";

    private static string GetSqlTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server schema tests.");
        }

        return connectionString;
    }

    private static IdentityDbContext CreateContext(
        string connectionString,
        params AppendOnlyAuditInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new IdentityDbContext(options);
    }

    private static async Task AssertMappedColumnsExist(
        DbConnection connection,
        IEntityType entityType,
        CancellationToken cancellationToken)
    {
        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException("Mapped table is required.");
        var schema = entityType.GetSchema()
            ?? throw new InvalidOperationException("Mapped schema is required.");
        var store = StoreObjectIdentifier.Table(table, schema);
        var columns = entityType.GetProperties()
            .Select(property => $"[{property.GetColumnName(store)}]");

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT TOP (0) {string.Join(", ", columns)} FROM [{schema}].[{table}];";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
