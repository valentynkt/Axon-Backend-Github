using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

public sealed class ChatReadDbContext : ReadDbContextBase<ChatModule>, IChatReadDbContext
{
    public ChatReadDbContext(DbContextOptions<ChatReadDbContext> options, ILogger<ChatReadDbContext>? logger = null) 
        : base(options, logger) { }

    public override string ModuleName => "chat";

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        base.ConfigureReadModelOptimizations(modelBuilder);
        
        // Add specific read model optimizations for Chat module
        var conversationEntity = modelBuilder.Entity<Conversation>();
        var messageEntity = modelBuilder.Entity<Message>();

        // Common query patterns - add indexes for frequent read operations
        conversationEntity.HasIndex(c => c.OwnerId)
            .HasDatabaseName("ix_conversations_owner_id");
        
        conversationEntity.HasIndex(c => new { c.OwnerId, c.Status })
            .HasDatabaseName("ix_conversations_owner_status");
        
        // Optimize for conversation listing with pagination
        conversationEntity.HasIndex(c => new { c.OwnerId, c.CreatedAt })
            .HasDatabaseName("ix_conversations_owner_created");
        
        // Message query optimizations already handled in MessageConfiguration
        // but add composite index for conversation message retrieval
        messageEntity.HasIndex(m => new { m.ConversationId, m.Sequence, m.CreatedAt })
            .HasDatabaseName("ix_messages_conversation_sequence_created");
    }
}