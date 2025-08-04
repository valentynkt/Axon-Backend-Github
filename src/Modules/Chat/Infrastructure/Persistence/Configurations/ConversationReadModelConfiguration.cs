using Axon.Modules.Chat.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ConversationReadModel with PostgreSQL optimizations
/// Following SPARC CQRS architecture patterns for denormalized read models
/// </summary>
public sealed class ConversationReadModelConfiguration : IEntityTypeConfiguration<ConversationReadModel>
{
    public void Configure(EntityTypeBuilder<ConversationReadModel> builder)
    {
        builder.ToTable("conversation_read_models");
        
        builder.HasKey(x => x.Id);
        
        // Primary key configuration
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        // Basic properties
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("title");
            
        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("status");

        builder.Property(x => x.MessageCount)
            .IsRequired()
            .HasColumnName("message_count")
            .HasDefaultValue(0);

        builder.Property(x => x.ToolExecutionCount)
            .IsRequired()
            .HasColumnName("tool_execution_count")
            .HasDefaultValue(0);

        builder.Property(x => x.LastMessageAt)
            .IsRequired()
            .HasColumnName("last_message_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        // JSON columns for flexible data using PostgreSQL JSONB
        builder.Property(x => x.Context)
            .HasColumnName("context")
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");
            
        builder.Property(x => x.Tags)
            .HasColumnName("tags")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");
            
        builder.Property(x => x.Participants)
            .HasColumnName("participants")
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        // Full-text search vector (computed column in PostgreSQL)
        builder.Property(x => x.SearchVector)
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('english', coalesce(title,'') || ' ' || coalesce(context::text,''))",
                stored: true);

        // Version control for read model synchronization
        builder.Property(x => x.Version)
            .IsRequired()
            .HasColumnName("version")
            .HasDefaultValue(0L)
            .IsConcurrencyToken();

        builder.Property(x => x.LastEventTimestamp)
            .IsRequired()
            .HasColumnName("last_event_timestamp")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()");

        builder.Property(x => x.LastProcessedEventId)
            .HasColumnName("last_processed_event_id")
            .HasMaxLength(100);

        // Configure audit fields using backing fields pattern
        ConfigureAuditFields(builder);

        // Performance indexes following SPARC requirements
        ConfigureIndexes(builder);
    }

    /// <summary>
    /// Configure audit fields using backing fields pattern per SPARC architecture
    /// </summary>
    private static void ConfigureAuditFields(EntityTypeBuilder<ConversationReadModel> builder)
    {
        builder.Property<DateTime>("_createdAtUtc")
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
            
        builder.Property<string>("_createdBy")
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property<DateTime>("_updatedAtUtc")
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
            
        builder.Property<string>("_updatedBy")
            .HasColumnName("updated_by")
            .HasMaxLength(100)
            .IsRequired();

        // Map property accessors to backing fields using HasField()
        builder.Property(e => e.CreatedAtUtc)
            .HasField("_createdAtUtc")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(e => e.CreatedBy)
            .HasField("_createdBy")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(e => e.UpdatedAtUtc)
            .HasField("_updatedAtUtc")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(e => e.UpdatedBy)
            .HasField("_updatedBy")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    /// <summary>
    /// Configure performance indexes for read model queries
    /// Following SPARC PostgreSQL optimization patterns
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<ConversationReadModel> builder)
    {
        // Full-text search index using GIN
        builder.HasIndex(x => x.SearchVector)
            .HasMethod("gin")
            .HasDatabaseName("ix_conversation_read_models_search_vector_gin");

        // Primary query optimization indexes
        builder.HasIndex(x => new { x.IsActive, x.LastMessageAt })
            .HasDatabaseName("ix_conversation_read_models_active_last_message")
            .HasFilter("is_active = true");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_conversation_read_models_status");

        // User-specific queries using backing field
        builder.HasIndex("_createdBy")
            .HasDatabaseName("ix_conversation_read_models_created_by");

        // Compound indexes for common query patterns
        builder.HasIndex(x => new { x.CreatedBy, x.IsActive, x.LastMessageAt })
            .HasDatabaseName("ix_conversation_read_models_user_active_last_message");

        // Version tracking for projections
        builder.HasIndex(x => new { x.Version, x.LastEventTimestamp })
            .HasDatabaseName("ix_conversation_read_models_version_timestamp");

        // Event processing tracking
        builder.HasIndex(x => x.LastProcessedEventId)
            .HasDatabaseName("ix_conversation_read_models_last_processed_event_id");

        // Time-based queries
        builder.HasIndex(x => x.LastMessageAt)
            .HasDatabaseName("ix_conversation_read_models_last_message_at");

        builder.HasIndex(x => x.CompletedAt)
            .HasDatabaseName("ix_conversation_read_models_completed_at");

        // JSON field indexes using GIN for flexible queries
        builder.HasIndex(x => x.Context)
            .HasMethod("gin")
            .HasDatabaseName("ix_conversation_read_models_context_gin");

        builder.HasIndex(x => x.Tags)
            .HasMethod("gin")
            .HasDatabaseName("ix_conversation_read_models_tags_gin");

        builder.HasIndex(x => x.Participants)
            .HasMethod("gin")
            .HasDatabaseName("ix_conversation_read_models_participants_gin");

        // Audit field indexes using backing field names
        builder.HasIndex("_createdAtUtc")
            .HasDatabaseName("ix_conversation_read_models_created_at_utc");

        builder.HasIndex("_updatedAtUtc")
            .HasDatabaseName("ix_conversation_read_models_updated_at_utc");

        // Statistics and analytics indexes
        builder.HasIndex(x => new { x.MessageCount, x.ToolExecutionCount })
            .HasDatabaseName("ix_conversation_read_models_message_tool_counts");

        // Active conversations by creation time
        builder.HasIndex("_createdBy", "_createdAtUtc")
            .HasDatabaseName("ix_conversation_read_models_user_created_at")
            .HasFilter("is_active = true");
    }
}