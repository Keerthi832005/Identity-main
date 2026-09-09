using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class BulkImportBatch
{
    private BulkImportBatch() { }

    public static BulkImportBatch Submit(
        Guid batchKey,
        string entityKey,
        int templateVersion,
        BulkImportSource source,
        string? fileName,
        long submittedByUserId,
        DateTime submittedAt,
        TimeSpan retention)
    {
        if (batchKey == Guid.Empty)
        {
            throw new ArgumentException("A batch key is required.", nameof(batchKey));
        }

        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention));
        }

        return new BulkImportBatch
        {
            BatchKey = batchKey,
            EntityKey = DomainRules.Required(entityKey, nameof(entityKey), 60),
            TemplateVersion = (int)DomainRules.Positive(templateVersion, nameof(templateVersion)),
            Source = source,
            FileName = DomainRules.Optional(fileName, nameof(fileName), 260),
            SubmittedByUserId = DomainRules.Positive(submittedByUserId, nameof(submittedByUserId)),
            SubmittedAt = submittedAt,
            ExpiresAt = submittedAt.Add(retention),
            State = BulkImportBatchState.Staged,
        };
    }

    /// <summary>
    /// Idempotent by design: a replayed commit returns false and changes nothing, so a retried
    /// request cannot write the same rows twice.
    /// </summary>
    public bool MarkCommitted(int createdRowCount, int updatedRowCount, DateTime committedAt)
    {
        if (State == BulkImportBatchState.Committed)
        {
            return false;
        }

        if (State != BulkImportBatchState.Staged)
        {
            throw new InvalidOperationException("Only a staged batch can be committed.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(createdRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(updatedRowCount);
        CreatedRowCount = createdRowCount;
        UpdatedRowCount = updatedRowCount;
        CommittedAt = committedAt;
        State = BulkImportBatchState.Committed;
        return true;
    }

    public void Discard()
    {
        if (State == BulkImportBatchState.Committed)
        {
            throw new InvalidOperationException("A committed batch cannot be discarded.");
        }

        State = BulkImportBatchState.Discarded;
    }

    public bool IsOwnedBy(long userId) => SubmittedByUserId == userId;

    public bool HasExpired(DateTime asOf) =>
        State == BulkImportBatchState.Staged && asOf >= ExpiresAt;

    public long BulkImportBatchId { get; private set; }
    public Guid BatchKey { get; private set; }
    public string EntityKey { get; private set; } = string.Empty;
    public int TemplateVersion { get; private set; }
    public BulkImportSource Source { get; private set; }
    public string? FileName { get; private set; }
    public long SubmittedByUserId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public BulkImportBatchState State { get; private set; }
    public DateTime? CommittedAt { get; private set; }
    public int CreatedRowCount { get; private set; }
    public int UpdatedRowCount { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
