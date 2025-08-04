using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Conversation aggregate following SPARC architecture patterns
/// Implements backing fields mapping, PostgreSQL optimizations, and snake_case naming
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        // Table configuration with snake_case naming
        builder.ToTable("conversations");
        
        // Primary key with ConversationId conversion as specified in SPARC
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(
                v => v.Value,
                v => ConversationId.From(v))
            .ValueGeneratedNever()
            .HasColumnName("id");

        // Title property configuration
        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("title")
            .HasColumnType("varchar(500)");

        // UserId property configuration for conversation ownership
        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("user_id")
            .HasColumnType("varchar(100)");

        // Status property with enum to string conversion
        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("status")
            .HasColumnType("varchar(50)");

        // CompletedAt nullable timestamp as specified in SPARC
        builder.Property(c => c.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        // Version property for optimistic concurrency control  
        // CRITICAL: Use PostgreSQL built-in xmin system column for proper row versioning
        // This avoids the need for a separate version column and automatically manages concurrency
        builder.Property(c => c.Version)
            .IsRowVersion();

        // Performance indexes and check constraints as specified in SPARC
        ConfigureIndexes(builder);
        ConfigureCheckConstraints(builder);

        // Configure domain events (ignore for persistence)
        builder.Ignore(c => c.DomainEvents);

        // Explicitly ignore the private _messages collection to prevent EF Core auto-detection
        builder.Ignore("_messages");
        
        // Ignore MessagesOrdered property to prevent navigation inference
        builder.Ignore(c => c.MessagesOrdered);
        
        // Audit fields are configured automatically via backing fields pattern in ChatDbContext
        // No need to configure them here as the global configuration handles IAuditable entities
    }

    /// <summary>
    /// Configure performance indexes for conversation queries
    /// Following SPARC PostgreSQL optimization patterns
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<Conversation> builder)
    {
        // Primary index for status queries (most common)
        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_conversations_status")
            .HasFilter("status IN ('Active', 'Completed')");

        // Index for user conversations with status filter
        builder.HasIndex(c => new { c.UserId, c.Status })
            .HasDatabaseName("ix_conversations_user_id_status")
            .HasFilter("status = 'Active'");

        // Composite index for completion queries with performance optimization
        builder.HasIndex(c => new { c.Status, c.CompletedAt })
            .HasDatabaseName("ix_conversations_status_completed_at")
            .HasFilter("completed_at IS NOT NULL");

        // Audit field indexes for temporal queries
        builder.HasIndex("_createdAtUtc")
            .HasDatabaseName("ix_conversations_created_at_utc");

        builder.HasIndex("_updatedAtUtc")
            .HasDatabaseName("ix_conversations_updated_at_utc");

        // Composite index for user activity tracking
        builder.HasIndex(nameof(Conversation.UserId), "_createdAtUtc")
            .HasDatabaseName("ix_conversations_user_id_created_at");

        // Index for title searches (supporting LIKE queries)
        builder.HasIndex(c => c.Title)
            .HasDatabaseName("ix_conversations_title")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        // Note: xmin system column doesn't need explicit indexing - it's automatically indexed by PostgreSQL
    }

    /// <summary>
    /// Configure check constraints as specified in SPARC
    /// </summary>
    private static void ConfigureCheckConstraints(EntityTypeBuilder<Conversation> builder)
    {
        // Check constraint for valid status values
        builder.HasCheckConstraint("ck_conversations_status_valid",
            "status IN ('Active', 'Completed', 'Archived')");

        // Check constraint for title length and content
        builder.HasCheckConstraint("ck_conversations_title_valid",
            "title IS NOT NULL AND LENGTH(TRIM(title)) > 0 AND LENGTH(title) <= 500");

        // Check constraint for completed_at logic
        builder.HasCheckConstraint("ck_conversations_completed_at_logic",
            "(status = 'Active' AND completed_at IS NULL) OR (status IN ('Completed', 'Archived') AND completed_at IS NOT NULL)");

        // Check constraint for audit fields
        builder.HasCheckConstraint("ck_conversations_audit_valid",
            "created_at_utc IS NOT NULL AND updated_at_utc IS NOT NULL AND updated_at_utc >= created_at_utc");
    }
}