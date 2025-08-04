using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.Configurations;
using Axon.Modules.Chat.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Design;

/// <summary>
/// Minimal DbContext for EF Core migrations without complex dependencies
/// Contains only entity configurations needed for schema generation
/// </summary>
public sealed class MinimalChatDbContext : DbContext
{
    /// <summary>
    /// Conversations aggregate root
    /// </summary>
    public DbSet<Conversation> Conversations { get; set; } = default!;

    /// <summary>
    /// Messages entity
    /// </summary>
    public DbSet<Message> Messages { get; set; } = default!;

    /// <summary>
    /// Outbox messages for Event Sourcing
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = default!;

    /// <summary>
    /// Dead letter messages for failed event processing
    /// </summary>
    public DbSet<DeadLetterMessage> DeadLetterMessages { get; set; } = default!;

    /// <summary>
    /// Event correlations for distributed tracing
    /// </summary>
    public DbSet<EventCorrelation> EventCorrelations { get; set; } = default!;

    // Note: ConversationReadModel excluded from minimal context due to complex audit field conflicts

    public MinimalChatDbContext(DbContextOptions<MinimalChatDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for Chat module
        modelBuilder.HasDefaultSchema("chat");

        // Configure entities manually without external configurations
        // This ensures we have full control over what gets configured
        ConfigureValueObjectConversions(modelBuilder);

        // Configure PostgreSQL extensions required for advanced features
        ConfigurePostgreSqlExtensions(modelBuilder);
    }

    /// <summary>
    /// Configures value object conversions to ensure EF Core recognizes them early
    /// </summary>
    private static void ConfigureValueObjectConversions(ModelBuilder modelBuilder)
    {
        // Configure Message entity
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasConversion(
                    v => v.Value,
                    v => MessageId.From(v))
                .HasColumnName("id");
            
            entity.Property(e => e.ConversationId)
                .HasConversion(
                    v => v.Value,
                    v => ConversationId.From(v))
                .HasColumnName("conversation_id");
            
            entity.Property(e => e.Content)
                .IsRequired()
                .HasColumnName("content");
                
            entity.Property(e => e.Role)
                .HasConversion(
                    v => v.Value,
                    v => v == "user" ? MessageRole.User : 
                        v == "assistant" ? MessageRole.Assistant :
                        v == "system" ? MessageRole.System :
                        v == "tool" ? MessageRole.Tool : MessageRole.User)
                .HasColumnName("role");
                
            entity.Property(e => e.Sequence)
                .HasColumnName("sequence");
            
            // Configure Metadata as JSONB to prevent navigation property interpretation
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
        });

        // Configure Conversation entity
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasConversion(
                    v => v.Value,
                    v => ConversationId.From(v))
                .HasColumnName("id");
                
            entity.Property(e => e.Title)
                .IsRequired()
                .HasColumnName("title");
                
            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("user_id");
                
            // Configure the relationship with Messages
            entity.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ConversationId)
                .HasConstraintName("fk_messages_conversation_id");
                
            // Ignore computed property to avoid EF Core confusion
            entity.Ignore(c => c.MessagesOrdered);
        });

        // Configure OutboxMessage entity
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
        });

        // Configure DeadLetterMessage entity
        modelBuilder.Entity<DeadLetterMessage>(entity =>
        {
            entity.ToTable("dead_letter_messages");
            entity.HasKey(e => e.Id);
        });

        // Configure EventCorrelation entity
        modelBuilder.Entity<EventCorrelation>(entity =>
        {
            entity.ToTable("event_correlations");
            entity.HasKey(e => e.Id);
        });
    }

    /// <summary>
    /// Configures PostgreSQL extensions required for advanced features
    /// </summary>
    private static void ConfigurePostgreSqlExtensions(ModelBuilder modelBuilder)
    {
        // Enable UUID generation extension
        modelBuilder.HasPostgresExtension("uuid-ossp");
        
        // Enable full-text search extension
        modelBuilder.HasPostgresExtension("pg_trgm");
        
        // Enable unaccent extension for better text search
        modelBuilder.HasPostgresExtension("unaccent");
        
        // Enable btree_gin extension for composite indexes
        modelBuilder.HasPostgresExtension("btree_gin");
        
        // Enable pg_stat_statements for query performance monitoring
        modelBuilder.HasPostgresExtension("pg_stat_statements");
    }
}