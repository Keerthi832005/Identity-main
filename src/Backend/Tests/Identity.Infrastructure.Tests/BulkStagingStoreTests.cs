using Identity.Application.BulkData;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Tests;

public sealed class BulkStagingStoreTests
{
    private static readonly IBulkRowSerializer Serializer = new BulkRowSerializer();

    private static IdentityDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    private static string? Connection() =>
        Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");

    private static async Task<long> SeedAdministrator(IdentityDbContext context)
    {
        var code = "BULK-" + Guid.NewGuid().ToString("N")[..12];
        var user = UserAccount.Create(code, "Bulk Test Administrator", DateTime.UtcNow);
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.UserId;
    }

    private static BulkImportRow StagedRow(
        long batchId,
        int number,
        string code,
        BulkImportRowState state,
        IReadOnlyList<BulkCellError>? errors = null) =>
        BulkImportRow.Stage(
            batchId,
            number,
            Serializer.SerializeValues(new Dictionary<string, string?>
            {
                ["employeeCode"] = code,
                ["displayName"] = "Someone",
            }),
            state == BulkImportRowState.Invalid ? Serializer.SerializeErrors(errors ?? []) : null,
            state);

    [Fact]
    public async Task StagedBatchPersistsEveryRowAndWritesNoIdentityRecord()
    {
        var connectionString = Connection();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the bulk staging test.");
        }

        await using var context = CreateContext(connectionString);
        var ownerId = await SeedAdministrator(context);
        var usersBefore = await context.UserAccounts.CountAsync(TestContext.Current.CancellationToken);

        var store = new BulkStagingStore(context);
        var batch = BulkImportBatch.Submit(
            Guid.NewGuid(),
            "users",
            1,
            BulkImportSource.Excel,
            "intake.xlsx",
            ownerId,
            DateTime.UtcNow,
            TimeSpan.FromHours(24));

        await store.AddBatch(
            batch,
            batchId =>
            [
                StagedRow(batchId, 3, "INDE05512", BulkImportRowState.Create),
                StagedRow(batchId, 4, "INDE04112", BulkImportRowState.Update),
                StagedRow(
                    batchId,
                    5,
                    "INDE05513",
                    BulkImportRowState.Invalid,
                    [new BulkCellError(5, "email", "email.invalid", "Not a valid email address.")]),
            ],
            TestContext.Current.CancellationToken);

        var summary = await store.Summarize(batch, TestContext.Current.CancellationToken);

        Assert.Equal(3, summary.TotalRows);
        Assert.Equal(1, summary.CreateRows);
        Assert.Equal(1, summary.UpdateRows);
        Assert.Equal(1, summary.InvalidRows);
        Assert.Equal(0, summary.AppliedRows);

        /* The whole point of staging: the identity tables are untouched until commit. */
        Assert.Equal(usersBefore, await context.UserAccounts.CountAsync(TestContext.Current.CancellationToken));

        await store.Remove(batch, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CorrectingARowClearsItsErrorsAndOnlyItsErrors()
    {
        var connectionString = Connection();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the bulk staging test.");
        }

        await using var context = CreateContext(connectionString);
        var ownerId = await SeedAdministrator(context);
        var store = new BulkStagingStore(context);
        var batch = BulkImportBatch.Submit(
            Guid.NewGuid(), "users", 1, BulkImportSource.Paste, null, ownerId,
            DateTime.UtcNow, TimeSpan.FromHours(24));

        await store.AddBatch(
            batch,
            batchId =>
            [
                StagedRow(
                    batchId, 3, "INDE05513", BulkImportRowState.Invalid,
                    [new BulkCellError(3, "email", "email.invalid", "Not a valid email address.")]),
                StagedRow(
                    batchId, 4, "INDE05514", BulkImportRowState.Invalid,
                    [new BulkCellError(4, "email", "email.invalid", "Not a valid email address.")]),
            ],
            TestContext.Current.CancellationToken);

        var row = await store.FindRow(
            batch.BulkImportBatchId, 3, TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        row.Correct(
            Serializer.SerializeValues(new Dictionary<string, string?>
            {
                ["employeeCode"] = "INDE05513",
                ["displayName"] = "Meera Nair",
            }),
            null,
            BulkImportRowState.Create,
            DateTime.UtcNow);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var summary = await store.Summarize(batch, TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.CreateRows);
        Assert.Equal(1, summary.InvalidRows);

        var untouched = await store.FindRow(
            batch.BulkImportBatchId, 4, TestContext.Current.CancellationToken);
        Assert.NotNull(untouched);
        Assert.Equal(BulkImportRowState.Invalid, untouched.State);
        Assert.NotNull(untouched.CellErrors);

        await store.Remove(batch, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ABatchIsInvisibleToAnyAdministratorButItsOwner()
    {
        var connectionString = Connection();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the bulk staging test.");
        }

        await using var context = CreateContext(connectionString);
        var ownerId = await SeedAdministrator(context);
        var otherId = await SeedAdministrator(context);
        var store = new BulkStagingStore(context);
        var batch = BulkImportBatch.Submit(
            Guid.NewGuid(), "users", 1, BulkImportSource.Excel, "intake.xlsx", ownerId,
            DateTime.UtcNow, TimeSpan.FromHours(24));
        await store.AddBatch(
            batch,
            batchId => [StagedRow(batchId, 3, "INDE05512", BulkImportRowState.Create)],
            TestContext.Current.CancellationToken);

        Assert.NotNull(await store.FindBatch(
            batch.BatchKey, ownerId, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindBatch(
            batch.BatchKey, otherId, TestContext.Current.CancellationToken));

        await store.Remove(batch, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void CommittingTheSameBatchTwiceAppliesItOnce()
    {
        var batch = BulkImportBatch.Submit(
            Guid.NewGuid(), "users", 1, BulkImportSource.Excel, "intake.xlsx", 1,
            DateTime.UtcNow, TimeSpan.FromHours(24));

        Assert.True(batch.MarkCommitted(219, 17, DateTime.UtcNow));
        Assert.False(batch.MarkCommitted(219, 17, DateTime.UtcNow));
        Assert.Equal(219, batch.CreatedRowCount);
        Assert.Equal(BulkImportBatchState.Committed, batch.State);
    }

    [Fact]
    public void ARowCannotBeStagedValidWhileCarryingErrors()
    {
        Assert.Throws<ArgumentException>(() => BulkImportRow.Stage(
            1, 3, "{}", "[{\"code\":\"x\"}]", BulkImportRowState.Create));

        Assert.Throws<ArgumentException>(() => BulkImportRow.Stage(
            1, 3, "{}", null, BulkImportRowState.Invalid));
    }
}
