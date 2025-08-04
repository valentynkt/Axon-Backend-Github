using System.Text.Json;
using System.Text.Json.Serialization;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Message entity following SPARC architecture patterns
/// Implements backing fields mapping, PostgreSQL optimizations, and snake_case naming
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        // Table configuration with snake_case naming
        builder.ToTable("messages");
        
        // Primary key with MessageId conversion as specified in SPARC
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(
                v => v.Value,
                v => MessageId.From(v))
            .ValueGeneratedNever()
            .HasColumnName("id");

        // ConversationId foreign key conversion as specified in SPARC
        builder.Property(m => m.ConversationId)
            .HasConversion(
                v => v.Value,
                v => ConversationId.From(v))
            .IsRequired()
            .HasColumnName("conversation_id");

        // Content property configuration
        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnName("content")
            .HasColumnType("text"); // PostgreSQL text type for unlimited length

        // Role property configuration
        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => MessageRole.FromValue(v))
            .HasMaxLength(50)
            .HasColumnName("role")
            .HasColumnType("varchar(50)");

        // Sequence property for message ordering
        builder.Property(m => m.Sequence)
            .IsRequired()
            .HasColumnName("sequence")
            .HasColumnType("integer");

        // Metadata as JSONB with Dictionary<string, JsonElement> mapping as specified in SPARC lines 293-299
        builder.Property(m => m.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, CreateJsonSerializerOptions()),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, CreateJsonSerializerOptions()));

        // Performance indexes and check constraints as specified in SPARC
        ConfigureIndexes(builder);
        ConfigureCheckConstraints(builder);

        // Full-text search configuration for message content
        ConfigureFullTextSearch(builder);

        // Audit fields are configured automatically via backing fields pattern in ChatDbContext
        // No need to configure them here as the global configuration handles IAuditable entities
    }

    /// <summary>
    /// Creates JsonSerializerOptions for metadata JSONB mapping as specified in SPARC
    /// </summary>
    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Configure performance indexes for message queries
    /// Following SPARC PostgreSQL optimization patterns
    /// </summary>
    private static void ConfigureIndexes(EntityTypeBuilder<Message> builder)
    {
        // Primary query index: conversation + sequence (for ordering) - unique constraint
        builder.HasIndex(m => new { m.ConversationId, m.Sequence })
            .HasDatabaseName("ix_messages_conversation_id_sequence")
            .IsUnique(); // Unique constraint on conversation + sequence as specified in SPARC

        // Index for conversation messages (most common query)
        builder.HasIndex(m => m.ConversationId)
            .HasDatabaseName("ix_messages_conversation_id");

        // Index for role-based queries with performance optimization
        builder.HasIndex(m => m.Role)
            .HasDatabaseName("ix_messages_role")
            .HasFilter("role IN ('user', 'assistant', 'system', 'tool')");

        // Composite index for conversation + role queries
        builder.HasIndex(m => new { m.ConversationId, m.Role })
            .HasDatabaseName("ix_messages_conversation_id_role");

        // GIN index for JSONB metadata searches as specified in SPARC
        builder.HasIndex(m => m.Metadata)
            .HasDatabaseName("ix_messages_metadata_gin")
            .HasMethod("gin");

        // Audit field indexes for temporal queries
        builder.HasIndex("_createdAtUtc")
            .HasDatabaseName("ix_messages_created_at_utc");

        builder.HasIndex("_updatedAtUtc")
            .HasDatabaseName("ix_messages_updated_at_utc");

        // Composite index for user activity tracking
        builder.HasIndex("_createdBy", "_createdAtUtc")
            .HasDatabaseName("ix_messages_created_by_created_at_utc");

        // Index for sequence-based ordering within conversations
        builder.HasIndex(m => new { m.ConversationId, "_createdAtUtc" })
            .HasDatabaseName("ix_messages_conversation_id_created_at_utc");
    }

    /// <summary>
    /// Configure check constraints as specified in SPARC
    /// </summary>
    private static void ConfigureCheckConstraints(EntityTypeBuilder<Message> builder)
    {
        // Check constraint for valid role values
        builder.HasCheckConstraint("ck_messages_role_valid",
            "role IN ('user', 'assistant', 'system', 'tool')");

        // Check constraint for content validation
        builder.HasCheckConstraint("ck_messages_content_valid",
            "content IS NOT NULL AND LENGTH(TRIM(content)) > 0 AND LENGTH(content) <= 100000");

        // Check constraint for sequence validation
        builder.HasCheckConstraint("ck_messages_sequence_valid",
            "sequence > 0");

        // Check constraint for conversation_id validation
        builder.HasCheckConstraint("ck_messages_conversation_id_valid",
            "conversation_id IS NOT NULL");

        // Check constraint for audit fields
        builder.HasCheckConstraint("ck_messages_audit_valid",
            "_created_at_utc IS NOT NULL AND _updated_at_utc IS NOT NULL AND _updated_at_utc >= _created_at_utc");
    }

    /// <summary>
    /// Configure full-text search capabilities for message content
    /// Using PostgreSQL tsvector and GIN indexes per SPARC requirements
    /// </summary>
    private static void ConfigureFullTextSearch(EntityTypeBuilder<Message> builder)
    {
        // Add computed tsvector column for full-text search
        builder.Property<string>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('english', content)", stored: true);

        // GIN index for full-text search performance
        builder.HasIndex("SearchVector")
            .HasDatabaseName("ix_messages_search_vector_gin")
            .HasMethod("gin");

        // Composite index for conversation-scoped full-text search
        builder.HasIndex(m => m.ConversationId, "SearchVector")
            .HasDatabaseName("ix_messages_conversation_search_gin")
            .HasMethod("gin");
    }
}