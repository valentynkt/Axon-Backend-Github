using Axon.Modules.Chat.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for OutboxMessage entity with PostgreSQL optimizations
/// Following SPARC Event Sourcing architecture patterns
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

/// <summary>
/// EF Core configuration for DeadLetterMessage entity
/// Part of SPARC error handling and resilience patterns
/// </summary>
public sealed class DeadLetterMessageConfiguration : IEntityTypeConfiguration<DeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        builder.ToTable("dead_letter_messages");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.OriginalMessageId)
            .IsRequired()
            .HasColumnName("original_message_id");
            
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("event_type");
            
        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnName("payload")
            .HasColumnType("jsonb");
            
        builder.Property(x => x.Metadata)
            .IsRequired()
            .HasColumnName("metadata")
            .HasColumnType("jsonb");
            
        builder.Property(x => x.FailureReason)
            .IsRequired()
            .HasColumnName("failure_reason")
            .HasColumnType("text");
            
        builder.Property(x => x.FailureStackTrace)
            .IsRequired()
            .HasColumnName("failure_stack_trace")
            .HasColumnType("text");
            
        builder.Property(x => x.ProcessingAttempts)
            .IsRequired()
            .HasColumnName("processing_attempts");
            
        builder.Property(x => x.OriginalOccurredAtUtc)
            .IsRequired()
            .HasColumnName("original_occurred_at_utc")
            .HasColumnType("timestamptz");
            
        builder.Property(x => x.MovedToDeadLetterAtUtc)
            .IsRequired()
            .HasColumnName("moved_to_dead_letter_at_utc")
            .HasColumnType("timestamptz");
            
        builder.Property(x => x.ReprocessedAtUtc)
            .HasColumnName("reprocessed_at_utc")
            .HasColumnType("timestamptz");
            
        builder.Property(x => x.IsReprocessed)
            .IsRequired()
            .HasColumnName("is_reprocessed")
            .HasDefaultValue(false);

        // Indexes for dead letter queue management
        ConfigureDeadLetterIndexes(builder);
    }

    private static void ConfigureDeadLetterIndexes(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        // Primary query index for unprocessed messages
        builder.HasIndex(x => x.IsReprocessed)
            .HasDatabaseName("ix_dead_letter_messages_is_reprocessed");

        // Time-based queries
        builder.HasIndex(x => x.MovedToDeadLetterAtUtc)
            .HasDatabaseName("ix_dead_letter_messages_moved_at_utc");

        // Event type analysis
        builder.HasIndex(x => x.EventType)
            .HasDatabaseName("ix_dead_letter_messages_event_type");

        // Original message tracking
        builder.HasIndex(x => x.OriginalMessageId)
            .HasDatabaseName("ix_dead_letter_messages_original_id");

        // Cleanup queries
        builder.HasIndex(x => new { x.IsReprocessed, x.MovedToDeadLetterAtUtc })
            .HasDatabaseName("ix_dead_letter_messages_cleanup");
    }
}

/// <summary>
/// EF Core configuration for EventCorrelation entity
/// Enables distributed tracing and debugging per SPARC patterns
/// </summary>
public sealed class EventCorrelationConfiguration : IEntityTypeConfiguration<EventCorrelation>
{
    public void Configure(EntityTypeBuilder<EventCorrelation> builder)
    {
        builder.ToTable("event_correlations");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("correlation_id");
            
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("event_type");
            
        builder.Property(x => x.AggregateId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("aggregate_id");
            
        builder.Property(x => x.CausationId)
            .HasMaxLength(100)
            .HasColumnName("causation_id");
            
        builder.Property(x => x.OccurredAtUtc)
            .IsRequired()
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamptz");
            
        builder.Property(x => x.MachineName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("machine_name");

        // Indexes for correlation tracking
        ConfigureCorrelationIndexes(builder);
    }

    private static void ConfigureCorrelationIndexes(EntityTypeBuilder<EventCorrelation> builder)
    {
        // Primary correlation tracking index
        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("ix_event_correlations_correlation_id");

        // Causation chain tracking
        builder.HasIndex(x => x.CausationId)
            .HasDatabaseName("ix_event_correlations_causation_id");

        // Time-based analysis
        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("ix_event_correlations_occurred_at_utc");

        // Event type analysis
        builder.HasIndex(x => x.EventType)
            .HasDatabaseName("ix_event_correlations_event_type");

        // Aggregate tracking
        builder.HasIndex(x => x.AggregateId)
            .HasDatabaseName("ix_event_correlations_aggregate_id");

        // Machine-based filtering
        builder.HasIndex(x => x.MachineName)
            .HasDatabaseName("ix_event_correlations_machine_name");
    }
}