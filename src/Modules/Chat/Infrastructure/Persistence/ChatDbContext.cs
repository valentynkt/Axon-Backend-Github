using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.Configurations;
using Axon.Modules.Chat.Infrastructure.Persistence.Entities;
using Axon.Modules.Chat.Infrastructure.Persistence.Interceptors;
using Axon.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;
using System.Reflection;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

/// <summary>
/// ChatDbContext implementing Event Sourcing and CQRS patterns with PostgreSQL optimizations
/// Features: outbox pattern, audit interceptor, domain event interceptor, backing fields mapping
/// Configured for high-performance concurrent operations with optimistic locking
/// </summary>
public sealed class ChatDbContext : DbContext
{
    private readonly AuditSaveChangesInterceptor _auditInterceptor;
    private readonly DomainEventInterceptor _domainEventInterceptor;

    /// <summary>
    /// Conversations aggregate root with domain events
    /// </summary>
    public DbSet<Conversation> Conversations { get; set; } = default!;

    /// <summary>
    /// Messages entity with conversation relationship
    /// </summary>
    public DbSet<Message> Messages { get; set; } = default!;

    /// <summary>
    /// Outbox messages for Event Sourcing transactional guarantees
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

    /// <summary>
    /// Conversation read models for CQRS query optimization
    /// Note: Temporarily commented out due to SearchVector tsvector mapping issues
    /// </summary>
    // public DbSet<ConversationReadModel> ConversationReadModels { get; set; } = default!;

    public ChatDbContext(
        DbContextOptions<ChatDbContext> options,
        AuditSaveChangesInterceptor auditInterceptor,
        DomainEventInterceptor domainEventInterceptor) : base(options)
    {
        _auditInterceptor = auditInterceptor;
        _domainEventInterceptor = domainEventInterceptor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // PostgreSQL configuration with resilience and performance optimizations
        optionsBuilder.UseNpgsql(options =>
        {
            // Connection resilience configuration
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
            
            // Command timeout configuration
            options.CommandTimeout(30);
        });

        // Add interceptors for audit and domain events
        optionsBuilder.AddInterceptors(_auditInterceptor, _domainEventInterceptor);

        // Configure PostgreSQL-specific optimizations
        ConfigurePostgreSqlOptimizations(optionsBuilder);

        // Configure warnings to suppress shadow property warnings
        optionsBuilder.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(CoreEventId.ShadowForeignKeyPropertyCreated);
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
        });

        // Development environment configurations
#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging(false); // Security: disabled even in debug
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for Chat module
        modelBuilder.HasDefaultSchema("chat");

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // CRITICAL: Configure explicit relationship handling to prevent shadow properties
        // This will be handled by explicit entity configurations instead of runtime navigation removal

        // Explicitly ignore ConversationSnapshot to prevent EF Core from treating it as an entity
        modelBuilder.Ignore<ConversationSnapshot>();
        
        // Configure audit backing fields for all auditable entities
        ConfigureAuditBackingFields(modelBuilder);

        // Configure PostgreSQL-specific naming conventions
        ConfigurePostgreSqlNamingConventions(modelBuilder);

        // Configure PostgreSQL extensions and optimizations
        ConfigurePostgreSqlExtensions(modelBuilder);

        // Configure full-text search for conversation content
        ConfigureFullTextSearch(modelBuilder);
    }

    /// <summary>
    /// Configures audit backing fields for all auditable entities
    /// Implements direct backing field access for maximum performance
    /// Maps audit properties to backing fields with proper PostgreSQL column types
    /// </summary>
    private static void ConfigureAuditBackingFields(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            
            // Configure backing fields for all IAuditable entities
            if (typeof(IAuditable).IsAssignableFrom(clrType))
            {
                var entityBuilder = modelBuilder.Entity(clrType);

                // CreatedAtUtc backing field configuration
                entityBuilder.Property<DateTime>("_createdAtUtc")
                    .HasColumnName("created_at_utc")
                    .HasColumnType("timestamptz")
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // CreatedBy backing field configuration
                entityBuilder.Property<string>("_createdBy")
                    .HasColumnName("created_by")
                    .HasMaxLength(100)
                    .IsRequired()
                    .HasDefaultValue("system");

                // UpdatedAtUtc backing field configuration
                entityBuilder.Property<DateTime>("_updatedAtUtc")
                    .HasColumnName("updated_at_utc")
                    .HasColumnType("timestamptz")
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // UpdatedBy backing field configuration
                entityBuilder.Property<string>("_updatedBy")
                    .HasColumnName("updated_by")
                    .HasMaxLength(100)
                    .IsRequired()
                    .HasDefaultValue("system");

                // EF Core will automatically map properties to backing fields

                // Create indexes for audit queries
                entityBuilder.HasIndex("_createdAtUtc")
                    .HasDatabaseName($"ix_{ToSnakeCase(entityType.GetTableName()!)}_created_at_utc");

                entityBuilder.HasIndex("_updatedAtUtc")
                    .HasDatabaseName($"ix_{ToSnakeCase(entityType.GetTableName()!)}_updated_at_utc");

                // Composite index for audit tracking
                entityBuilder.HasIndex(new[] { "_createdBy", "_createdAtUtc" })
                    .HasDatabaseName($"ix_{ToSnakeCase(entityType.GetTableName()!)}_created_audit");
            }

            // NOTE: Removed shadow property Version configuration to avoid conflicts
            // The Version property is now explicitly configured in entity configurations
            // This prevents EF Core confusion between explicit and shadow properties
        }
    }

    /// <summary>
    /// Configures PostgreSQL-specific optimizations including connection pooling,
    /// query splitting, and performance enhancements for concurrent operations
    /// </summary>
    private static void ConfigurePostgreSqlOptimizations(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(npgsqlOptions =>
        {
            // Configure connection timeout
            npgsqlOptions.CommandTimeout(30);
        });

        // Enable compiled model for better startup performance
        optionsBuilder.EnableServiceProviderCaching();
        
        // Configure query behavior for better performance
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        
        // Configure warnings as needed
        optionsBuilder.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
        });
    }

    /// <summary>
    /// Override SaveChangesAsync to handle domain events and outbox pattern
    /// Implements transactional consistency for event sourcing architecture
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Capture domain events before saving
        var domainEvents = GetDomainEvents();

        // Execute save changes with audit and domain event handling
        var result = await base.SaveChangesAsync(cancellationToken);

        // Domain events are handled by the DomainEventInterceptor
        // This ensures transactional consistency with the outbox pattern

        return result;
    }

    /// <summary>
    /// Configures PostgreSQL naming conventions to snake_case
    /// </summary>
    private static void ConfigurePostgreSqlNamingConventions(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Convert table names to snake_case
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            // Convert column names to snake_case
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrEmpty(columnName))
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            // Convert foreign key names to snake_case
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var constraintName = foreignKey.GetConstraintName();
                if (!string.IsNullOrEmpty(constraintName))
                {
                    foreignKey.SetConstraintName($"fk_{ToSnakeCase(constraintName.Replace("FK_", ""))}");
                }
            }

            // Convert index names to snake_case
            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (!string.IsNullOrEmpty(indexName))
                {
                    index.SetDatabaseName($"ix_{ToSnakeCase(indexName.Replace("IX_", ""))}");
                }
            }
        }
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

    /// <summary>
    /// Configures full-text search capabilities for conversation content
    /// Note: Temporarily disabled due to EF Core tsvector mapping limitations
    /// </summary>
    private static void ConfigureFullTextSearch(ModelBuilder modelBuilder)
    {
        // Configure full-text search for conversation read models (commented out for now)
        // modelBuilder.Entity<ConversationReadModel>(entity =>
        // {
        //     // Create GIN index for full-text search
        //     entity.HasIndex(e => e.SearchVector)
        //         .HasMethod("GIN")
        //         .HasDatabaseName("ix_conversation_read_models_search_vector");
        //
        //     // Configure search vector as computed column
        //     entity.Property(e => e.SearchVector)
        //         .HasColumnType("text")
        //         .HasComputedColumnSql("to_tsvector('english', title || ' ' || context)", stored: true);
        // });
        
        // Suppress unused parameter warning
        _ = modelBuilder;
    }

    /// <summary>
    /// Gets domain events from aggregate roots for outbox pattern processing
    /// </summary>
    private IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        return ChangeTracker.Entries<IAggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToList();
    }

    /// <summary>
    /// Converts PascalCase to snake_case for PostgreSQL naming conventions
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        var previousChar = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            var currentChar = input[i];

            if (char.IsUpper(currentChar) && i > 0 && 
                previousChar != '_' && !char.IsUpper(previousChar))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(currentChar));
            previousChar = currentChar;
        }

        return result.ToString();
    }

    /// <summary>
    /// Compiled queries for high-performance data access
    /// Implements query optimization patterns for CQRS read operations
    /// </summary>
    public static class CompiledQueries
    {
        /// <summary>
        /// Get conversation by ID with messages - compiled for performance
        /// </summary>
        public static readonly Func<ChatDbContext, ConversationId, IAsyncEnumerable<Conversation>> GetConversationById =
            EF.CompileAsyncQuery((ChatDbContext context, ConversationId id) =>
                context.Conversations
                    .AsSplitQuery()
                    .Where(c => c.Id == id));

        /// <summary>
        /// Get conversations by user with pagination - compiled for performance
        /// </summary>
        public static readonly Func<ChatDbContext, string, int, int, IAsyncEnumerable<Conversation>> GetConversationsByUser =
            EF.CompileAsyncQuery((ChatDbContext context, string userId, int skip, int take) =>
                context.Conversations
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => EF.Property<DateTime>(c, "_updatedAtUtc"))
                    .Skip(skip)
                    .Take(take));

        /// <summary>
        /// Get unprocessed outbox messages for event processing - compiled for performance
        /// </summary>
        public static readonly Func<ChatDbContext, int, IAsyncEnumerable<OutboxMessage>> GetUnprocessedOutboxMessages =
            EF.CompileAsyncQuery((ChatDbContext context, int batchSize) =>
                context.OutboxMessages
                    .Where(o => o.ProcessedAtUtc == null)
                    .Where(o => o.NextRetryAtUtc == null || o.NextRetryAtUtc <= DateTime.UtcNow)
                    .OrderBy(o => o.OccurredAtUtc)
                    .Take(batchSize));

        /// <summary>
        /// Full-text search conversations - compiled for performance
        /// Note: Temporarily commented out due to ConversationReadModel SearchVector issues
        /// </summary>
        // public static readonly Func<ChatDbContext, string, int, IAsyncEnumerable<ConversationReadModel>> SearchConversations =
        //     EF.CompileAsyncQuery((ChatDbContext context, string searchTerm, int take) =>
        //         context.ConversationReadModels
        //             .Where(c => c.IsActive)
        //             .Where(c => EF.Functions.ToTsVector("english", c.Title + " " + c.Context).Matches(EF.Functions.ToTsQuery("english", searchTerm)))
        //             .OrderByDescending(c => c.LastMessageAt)
        //             .Take(take));
    }
}