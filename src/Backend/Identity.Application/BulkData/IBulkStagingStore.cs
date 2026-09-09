using Identity.Domain.Entities;

namespace Identity.Application.BulkData;

/// <summary>
/// Persistence for staged batches. Every read is scoped to the submitting administrator, so a batch
/// key from another administrator is indistinguishable from one that does not exist.
/// </summary>
public interface IBulkStagingStore
{
    /// <summary>
    /// Rows are produced from the persisted batch id, because a row cannot exist before the batch it
    /// belongs to. The store saves the batch, then builds and saves its rows.
    /// </summary>
    Task<BulkImportBatch> AddBatch(
        BulkImportBatch batch,
        Func<long, IReadOnlyList<BulkImportRow>> rows,
        CancellationToken cancellationToken);

    Task<BulkImportBatch?> FindBatch(Guid batchKey, long ownerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BulkImportRow>> ListRows(
        long batchId,
        CancellationToken cancellationToken);

    Task<BulkImportRow?> FindRow(long batchId, int sourceRowNumber, CancellationToken cancellationToken);

    Task<BulkBatchSummary> Summarize(BulkImportBatch batch, CancellationToken cancellationToken);

    Task<IReadOnlyList<BulkImportBatch>> ListExpired(DateTime asOf, int take, CancellationToken cancellationToken);

    Task Remove(BulkImportBatch batch, CancellationToken cancellationToken);
}
