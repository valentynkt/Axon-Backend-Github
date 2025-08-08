using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// EF Core configuration for centralized OutboxMessage entity with PostgreSQL optimizations.
/// Moved from Chat module to BuildingBlocks for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Enhanced with comprehensive indexing strategy following SPARC Event Sourcing architecture patterns.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("type");
            
        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnName("payload")
            .HasColumnType("jsonb"); // PostgreSQL JSONB for efficient storage
            
        builder.Property(x => x.Metadata)
            .IsRequired()
            .HasColumnName("metadata")
            .HasColumnType("jsonb");
            
        builder.Property(x => x.OccurredAtUtc)
            .IsRequired()
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamptz");
            
        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ProcessingAttempts)
            .IsRequired()
            .HasColumnName("processing_attempts")
            .HasDefaultValue(0);

        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");
            
        builder.Property(x => x.NextRetryAtUtc)
            .HasColumnName("next_retry_at_utc")
            .HasColumnType("timestamptz");
        
        // Critical indexes for performance following SPARC patterns
        ConfigureIndexes(builder);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Primary processing index - FOR UPDATE SKIP LOCKED queries
        builder.HasIndex(x => x.ProcessedAtUtc)
            .HasDatabaseName("ix_outbox_messages_processed_at_utc");
            
        // Composite index for worker queries with partial index
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.NextRetryAtUtc })
            .HasDatabaseName("ix_outbox_messages_processing")
            .HasFilter("processed_at_utc IS NULL");
            
        // Time-based index for monitoring
        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("ix_outbox_messages_occurred_at_utc");
            
        // Monitoring index for old unprocessed messages
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc })
            .HasDatabaseName("ix_outbox_messages_processed_occurred")
            .HasFilter("processed_at_utc IS NULL");

        // Event type index for debugging and monitoring
        builder.HasIndex(x => x.Type)
            .HasDatabaseName("ix_outbox_messages_type");

        // Retry processing index
        builder.HasIndex(x => new { x.ProcessingAttempts, x.NextRetryAtUtc })
            .HasDatabaseName("ix_outbox_messages_retry_processing")
            .HasFilter("processed_at_utc IS NULL AND next_retry_at_utc IS NOT NULL");
    }
}