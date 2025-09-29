using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

public sealed class ChatReadDbContext : ReadDbContextBase<ChatModule>, IChatReadDbContext
{
    public ChatReadDbContext(DbContextOptions<ChatReadDbContext> options, ILogger<ChatReadDbContext>? logger = null)
        : base(options, logger)
    {
        // Additional read-specific optimizations
        Database.SetCommandTimeout(TimeSpan.FromSeconds(30)); // 30-second timeout for read operations
    }

    public override string ModuleName => "chat";

    // Expose Conversations for direct query access in read operations
    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply the same entity configurations as ChatDbContext to ensure consistent schema
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
    }

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
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

        // Call base implementation for standard timestamp indexes
        base.ConfigureReadModelOptimizations(modelBuilder);
    }
}