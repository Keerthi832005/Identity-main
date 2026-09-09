using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Configurations;

internal static class BulkDataModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var batch = modelBuilder.Entity<BulkImportBatch>();
        batch.ToTable("BulkImportBatch", "Identity");
        batch.HasKey(x => x.BulkImportBatchId).HasName("PkBulkImportBatch");
        batch.HasIndex(x => x.BatchKey).IsUnique().HasDatabaseName("UqBulkImportBatchKey");
        batch.Property(x => x.EntityKey).HasMaxLength(60);
        batch.Property(x => x.Source).HasConversion<string>().HasMaxLength(10);
        batch.Property(x => x.State).HasConversion<string>().HasMaxLength(12);
        batch.Property(x => x.FileName).HasMaxLength(260);
        batch.Property(x => x.RowVersion).IsRowVersion();
        batch.HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.NoAction);
        batch.HasIndex(x => new { x.SubmittedByUserId, x.State, x.SubmittedAt })
            .HasDatabaseName("IxBulkImportBatchOwner");

        var row = modelBuilder.Entity<BulkImportRow>();
        row.ToTable("BulkImportRow", "Identity");
        row.HasKey(x => x.BulkImportRowId).HasName("PkBulkImportRow");
        row.HasIndex(x => new { x.BulkImportBatchId, x.SourceRowNumber }).IsUnique()
            .HasDatabaseName("UqBulkImportRowNumber");
        row.Property(x => x.State).HasConversion<string>().HasMaxLength(10);
        row.HasOne<BulkImportBatch>().WithMany()
            .HasForeignKey(x => x.BulkImportBatchId).OnDelete(DeleteBehavior.Cascade);
        row.HasIndex(x => new { x.BulkImportBatchId, x.State, x.SourceRowNumber })
            .HasDatabaseName("IxBulkImportRowBatchState");
    }
}
