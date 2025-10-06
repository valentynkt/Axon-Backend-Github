using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Unified DbContext for Chat module - handles both read and write operations.
/// Inherits from DbContextBase which provides both write capabilities and read optimizations.
/// </summary>
public sealed class ChatDbContext : DbContextBase<ChatModule>, IChatDbContext
{
    private readonly TimeProvider _timeProvider;

    public ChatDbContext(
        DbContextOptions<ChatDbContext> options,
        TimeProvider timeProvider,
        ILogger<ChatDbContext>? logger = null)
        : base(options, logger)
    {
        _timeProvider = timeProvider;
        // Command timeout and Query<T>() now inherited from DbContextBase
    }

    public override string ModuleName => "chat";

    public DbSet<Conversation> Conversations => Set<Conversation>();

    // Query<T>() method now inherited from DbContextBase

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);

        // Explicitly apply configurations to ensure they work in test contexts
        // The assembly scan might not work correctly in some test scenarios
        var conversationConfig = new Persistence.Configurations.ConversationConfiguration();
        conversationConfig.Configure(modelBuilder.Entity<Conversation>());

        // Messages are now configured as owned entities within ConversationConfiguration
        // They are accessed through the Conversation aggregate root, not directly

        // Remove query filters from owned entities (EF Core doesn't support filters on owned types)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                // Clear any query filter that was automatically applied
                entityType.SetQueryFilter(null);
            }
        }

        // Apply read optimizations (indexes for query performance)
        ConfigureReadOptimizations(modelBuilder);
    }

    /// <summary>
    /// Configure read-optimized indexes for query performance.
    /// These indexes improve performance for common read queries without affecting write operations.
    /// Merged from ChatReadDbContext's ConfigureReadModelOptimizations.
    /// </summary>
    private static void ConfigureReadOptimizations(ModelBuilder modelBuilder)
    {
        // Essential indexes for GetConversations query performance
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.OwnerId, c.UpdatedAt, c.Id })
            .HasDatabaseName("ix_conversations_owner_updated_at_id");

        // Title search index for filtering by conversation title
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.Title)
            .HasDatabaseName("ix_conversations_title_search")
            .HasFilter("\"Title\" IS NOT NULL");

        // Additional composite index for different sorting scenarios
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.OwnerId, c.Status, c.UpdatedAt })
            .HasDatabaseName("ix_conversations_owner_status_updated");

        // CreatedAt index for sorting by creation time
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.OwnerId, c.CreatedAt, c.Id })
            .HasDatabaseName("ix_conversations_owner_created_at_id")
            .HasFilter("\"Status\" != 'Deleted'");
    }

    protected override void ApplyAuditInformation()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    // For new entities, set both CreatedAt and UpdatedAt using reflection
                    // since we can't cast to the generic type without knowing TId
                    SetAuditTimestamp(auditableEntity, "SetCreatedAtInternal", now);
                    SetAuditTimestamp(auditableEntity, "SetUpdatedAtInternal", now);
                }
                else if (entry.State == EntityState.Modified)
                {
                    // For modified entities, only update UpdatedAt
                    SetAuditTimestamp(auditableEntity, "SetUpdatedAtInternal", now);
                }
            }
        }
    }

    private static void SetAuditTimestamp(IAuditable entity, string methodName, DateTimeOffset timestamp)
    {
        var method = entity.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(entity, new object[] { timestamp });
    }
}

/// <summary>
/// Design-time factory for ChatDbContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class ChatDbContextFactory : DesignTimeDbContextFactoryBase<ChatDbContext>
{
    protected override ChatDbContext CreateNewInstance(DbContextOptions<ChatDbContext> options) =>
        new(options, TimeProvider.System);

    protected override void ConfigureProvider(DbContextOptionsBuilder<ChatDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, opt =>
        {
            opt.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
            opt.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
        });
}