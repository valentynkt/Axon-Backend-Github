using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxEntryConfiguration : IEntityTypeConfiguration<OutboxEntry>
{
    public void Configure(EntityTypeBuilder<OutboxEntry> builder)
    {
        builder.ToTable("outbox_entries", schema: "shared");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => OutboxEntryId.From(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TransactionId)
            .IsRequired()
            .HasColumnName("transaction_id");

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("event_type");

        builder.Property(e => e.EventData)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("event_data");

        builder.Property(e => e.TraceId)
            .HasMaxLength(32)
            .HasColumnName("trace_id");

        builder.Property(e => e.RequestId)
            .HasColumnName("request_id");

        builder.Property(e => e.TenantId)
            .HasMaxLength(100)
            .HasColumnName("tenant_id");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.ProcessingStartedAt)
            .HasColumnName("processing_started_at");

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasColumnName("status")
            .HasConversion<int>();

        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .HasMaxLength(2000)
            .HasColumnName("last_error");

        builder.Property(e => e.NextRetryAt)
            .HasColumnName("next_retry_at");

        builder.Property(e => e.Metadata)
            .HasColumnType("text")
            .HasColumnName("metadata");

        builder.Property(e => e.Version)
            .IsRequired()
            .HasColumnName("version")
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        // ---- Indexes (filters now use actual column names) ----

        // Primary processing index (Pending=0, Failed=3)
        builder.HasIndex(e => new { e.Status, e.NextRetryAt, e.CreatedAt })
            .HasDatabaseName("ix_outbox_entries_processing")
            .HasFilter("status IN (0, 3)");

        // Transaction-scoped lookups
        builder.HasIndex(e => e.TransactionId)
            .HasDatabaseName("ix_outbox_entries_transaction_id");

        // Tenant-scoped queries
        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("ix_outbox_entries_tenant_status")
            .HasFilter("tenant_id IS NOT NULL");

        // Trace correlation
        builder.HasIndex(e => e.TraceId)
            .HasDatabaseName("ix_outbox_entries_trace_id")
            .HasFilter("trace_id IS NOT NULL");

        // Type filtering
        builder.HasIndex(e => new { e.EventType, e.Status })
            .HasDatabaseName("ix_outbox_entries_event_type_status");

        // Cleanup (Completed=2, DeadLetter=4)
        builder.HasIndex(e => new { e.Status, e.ProcessedAt })
            .HasDatabaseName("ix_outbox_entries_cleanup")
            .HasFilter("status IN (2, 4)");

        // Stuck processing (Processing=1)
        builder.HasIndex(e => new { e.Status, e.ProcessingStartedAt })
            .HasDatabaseName("ix_outbox_entries_stuck_processing")
            .HasFilter("status = 1");
    }
}
