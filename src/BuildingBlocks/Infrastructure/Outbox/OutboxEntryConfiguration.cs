using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BuildingBlocks.Core.Domain.Primitives.Serialization;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Entity Framework configuration for OutboxEntry
/// Optimized for high-throughput event processing with proper indexing
/// </summary>
public sealed class OutboxEntryConfiguration : IEntityTypeConfiguration<OutboxEntry>
{
    public void Configure(EntityTypeBuilder<OutboxEntry> builder)
    {
        // Table configuration
        builder.ToTable("outbox_entries", schema: "shared");
        
        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => OutboxEntryId.From(value))
            .ValueGeneratedNever();

        // Transaction ID - critical for transaction-scoped processing
        builder.Property(e => e.TransactionId)
            .IsRequired()
            .HasColumnName("transaction_id");

        // Event Type - frequently queried for filtering
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("event_type");

        // Event Data - JSON payload, can be large
        builder.Property(e => e.EventData)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("event_data");

        // Trace ID for correlation
        builder.Property(e => e.TraceId)
            .HasMaxLength(32)
            .HasColumnName("trace_id");

        // Request ID for correlation
        builder.Property(e => e.RequestId)
            .HasColumnName("request_id");

        // Tenant ID for multi-tenant scenarios
        builder.Property(e => e.TenantId)
            .HasMaxLength(100)
            .HasColumnName("tenant_id");

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.ProcessingStartedAt)
            .HasColumnName("processing_started_at");

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        // Status - critical for queries
        builder.Property(e => e.Status)
            .IsRequired()
            .HasColumnName("status")
            .HasConversion<int>();

        // Retry tracking
        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.LastError)
            .HasMaxLength(2000)
            .HasColumnName("last_error");

        builder.Property(e => e.NextRetryAt)
            .HasColumnName("next_retry_at");

        // Metadata as JSON
        builder.Property(e => e.Metadata)
            .HasColumnType("text")
            .HasColumnName("metadata");

        // Version for optimistic concurrency
        builder.Property(e => e.Version)
            .IsRequired()
            .HasColumnName("version")
            .HasDefaultValue(1)
            .IsConcurrencyToken();

        // Indexes for optimal query performance
        
        // Primary index for processing pending entries
        builder.HasIndex(e => new { e.Status, e.NextRetryAt, e.CreatedAt })
            .HasDatabaseName("ix_outbox_entries_processing")
            .HasFilter($"{nameof(OutboxEntry.Status)} IN (0, 3)"); // Pending, Failed

        // Index for transaction-scoped queries
        builder.HasIndex(e => e.TransactionId)
            .HasDatabaseName("ix_outbox_entries_transaction_id");

        // Index for tenant-scoped queries (if using multi-tenancy)
        builder.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("ix_outbox_entries_tenant_status")
            .HasFilter($"{nameof(OutboxEntry.TenantId)} IS NOT NULL");

        // Index for trace correlation
        builder.HasIndex(e => e.TraceId)
            .HasDatabaseName("ix_outbox_entries_trace_id")
            .HasFilter($"{nameof(OutboxEntry.TraceId)} IS NOT NULL");

        // Index for event type filtering
        builder.HasIndex(e => new { e.EventType, e.Status })
            .HasDatabaseName("ix_outbox_entries_event_type_status");

        // Index for cleanup operations (completed/dead letter entries)
        builder.HasIndex(e => new { e.Status, e.ProcessedAt })
            .HasDatabaseName("ix_outbox_entries_cleanup")
            .HasFilter($"{nameof(OutboxEntry.Status)} IN (2, 4)"); // Completed, DeadLetter

        // Index for stuck processing detection
        builder.HasIndex(e => new { e.Status, e.ProcessingStartedAt })
            .HasDatabaseName("ix_outbox_entries_stuck_processing")
            .HasFilter($"{nameof(OutboxEntry.Status)} = 1"); // Processing

        // Unique constraint to prevent duplicate events (if needed)
        // This is optional and depends on business requirements
        // builder.HasIndex(e => new { e.EventType, e.EventData })
        //     .IsUnique()
        //     .HasDatabaseName("ix_outbox_entries_deduplication");
    }
}