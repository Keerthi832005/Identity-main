using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.BulkData;
using Identity.Application.Messaging;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class OrganizationBulkCommitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MixedBatchUpdatesExistingRootCreates101ChildrenAndReplaysWithoutDuplicates()
    {
        await using var provider = Provider();
        var actor = await Actor(provider);
        var root = await Send(provider, new CreateOrganizationCommand("BULK-ROOT", "Before", actor));
        var rows = Enumerable.Range(1, 101)
            .Select(index => Row(index + 3, "Country", $"BULK-C{index}", "BULK-ROOT"))
            .Append(Row(3, "Organization", "BULK-ROOT", null, "After"))
            .ToArray();
        var batch = await Send(provider, new StageBulkBatchCommand(
            OrganizationBulkDescriptor.EntityKey, BulkImportSource.Paste, null, rows, actor));
        Assert.Equal(101, batch.CreateRows);
        Assert.Equal(1, batch.UpdateRows);
        Assert.Equal(0, batch.InvalidRows);

        var lastPage = await Send(provider, new GetBulkBatchQuery(batch.BatchKey, 100, 25, BulkRowFilter.All, actor));
        Assert.Equal(102, lastPage.TotalCount);
        Assert.Equal(2, lastPage.Rows.Count);

        var applied = await Send(provider, new CommitBulkBatchCommand(batch.BatchKey, actor));
        Assert.Equal(101, applied.CreatedRowCount);
        Assert.Equal(1, applied.UpdatedRowCount);
        var updatedRoot = await Send(provider, new GetOrganizationUnitQuery(root.OrganizationId, root.OrganizationUnitId, actor));
        Assert.Equal("After", updatedRoot.UnitName);
        Assert.Equal(root.HierarchyPath, updatedRoot.HierarchyPath);
        var replay = await Send(provider, new CommitBulkBatchCommand(batch.BatchKey, actor));
        Assert.True(replay.AlreadyCommitted);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await db.OrganizationUnits.CountAsync(x => x.UnitCode == "BULK-ROOT", Token));
        Assert.Equal(102, await db.OrganizationUnits.CountAsync(x => x.OrganizationId == root.OrganizationId, Token));
    }

    [Fact]
    public async Task UpdatingChildPreservesParentAndRejectedMoveRollsBackTheWholeBatch()
    {
        await using var provider = Provider();
        var actor = await Actor(provider);
        var root = await Send(provider, new CreateOrganizationCommand("EDIT-ROOT", "Root", actor));
        var other = await Send(provider, new CreateOrganizationCommand("OTHER-ROOT", "Other", actor));
        var child = await Send(provider, new CreateOrganizationUnitCommand(root.OrganizationId, root.OrganizationUnitId,
            OrganizationUnitType.Country, "EDIT-CHILD", "Before", actor));
        var batch = await Send(provider, new StageBulkBatchCommand(OrganizationBulkDescriptor.EntityKey,
            BulkImportSource.Paste, null, [Row(3, "Country", "EDIT-CHILD", "EDIT-ROOT", "Updated")], actor));
        var result = await Send(provider, new CommitBulkBatchCommand(batch.BatchKey, actor));
        Assert.Equal(1, result.UpdatedRowCount);
        var updated = await Send(provider, new GetOrganizationUnitQuery(root.OrganizationId, child.OrganizationUnitId, actor));
        Assert.Equal("Updated", updated.UnitName);
        Assert.Equal(root.OrganizationUnitId, updated.ParentOrganizationUnitId);

        var invalid = await Send(provider, new StageBulkBatchCommand(OrganizationBulkDescriptor.EntityKey,
            BulkImportSource.Paste, null,
            [Row(3, "Country", "ROLLBACK-CHILD", "EDIT-ROOT"), Row(4, "Country", "EDIT-CHILD", "OTHER-ROOT")], actor));
        await Assert.ThrowsAsync<AdministrationException>(() => Send(provider, new CommitBulkBatchCommand(invalid.BatchKey, actor)));
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await db.OrganizationUnits.AnyAsync(x => x.UnitCode == "ROLLBACK-CHILD", Token));
        var stillStaged = await Send(provider, new GetBulkBatchQuery(invalid.BatchKey, 0, 25, BulkRowFilter.All, actor));
        Assert.Equal(BulkImportBatchState.Staged, stillStaged.Summary.State);
        Assert.Equal(0, stillStaged.Summary.AppliedRows);
        Assert.NotEqual(root.OrganizationId, other.OrganizationId);
    }

    private static BulkRow Row(int number, string type, string code, string? parent, string? name = null) =>
        new(number, [
            new BulkCell("unitType", type, type, false),
            new BulkCell("unitCode", code, code, false),
            new BulkCell("unitName", name ?? code, name ?? code, false),
            new BulkCell("parentUnitCode", parent, parent, false),
        ]);

    private static ServiceProvider Provider()
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Set an isolated FIN_IAM_BulkTests_ SQL connection.");
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
        Assert.StartsWith("FIN_IAM_BulkTests_", target.InitialCatalog);
        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connection);
        services.AddSingleton<IAdministrationAuthorizer>(new TestAuthorizer());
        return services.BuildServiceProvider();
    }

    private static async Task<AdministrationContext> Actor(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = UserAccount.Create("BULK-" + Guid.NewGuid().ToString("N")[..12], "Test administrator", DateTime.UtcNow);
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync(Token);
        return new(user.UserId, Guid.NewGuid());
    }

    private static async Task<T> Send<T>(ServiceProvider provider, IRequest<T> request)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().Send(request, Token);
    }

    private sealed class TestAuthorizer : IAdministrationAuthorizer
    {
        public ValueTask Authorize(AdministrationContext context, AdministrationAction action, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
