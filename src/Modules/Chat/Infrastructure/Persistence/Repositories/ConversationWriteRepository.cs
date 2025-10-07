using System.Linq;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Axon.Modules.Chat.Domain.Entities;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Write repository for Conversation aggregate.
/// Handles create, update, delete operations with change tracking.
/// For read-only queries, use IConversationReadRepository.
/// </summary>
public sealed class ConversationWriteRepository : EfWriteRepository<Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork<ChatModule> _unitOfWork;
    private readonly ChatDbContext _chatDbContext;

    public ConversationWriteRepository(ChatDbContext context, IWriteUnitOfWork<ChatModule> unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
        _chatDbContext = context;
    }

    public IWriteUnitOfWork<ChatModule> UnitOfWork => _unitOfWork;

    /// <summary>
    /// Override GetByIdAsync to include Messages navigation property via private field.
    /// Uses AsSplitQuery to avoid Cartesian explosion with large message collections.
    /// CRITICAL FIX: Uses AsNoTracking() to bypass EF Core's identity map and get fresh data from DB.
    /// This prevents stale owned entities (Messages) from being returned when the same conversation
    /// is loaded multiple times (e.g., in E2E tests with multiple HTTP requests).
    /// After loading, we attach the entity to enable change tracking for updates.
    /// </summary>
    public override async Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken ct = default)
    {
        // First check if the entity is already tracked in the current context
        var trackedEntity = _chatDbContext.ChangeTracker
            .Entries<Conversation>()
            .FirstOrDefault(e => e.Entity.Id == id)
            ?.Entity;

        if (trackedEntity != null)
        {
            // Entity is already tracked, return it (it may have pending changes)
            return trackedEntity;
        }

        // Use AsNoTracking to get fresh data from DB, avoiding stale cached entities
        // This is critical for E2E tests where multiple requests operate on same conversation
        var conversation = await _chatDbContext.Set<Conversation>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include("_messages")
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation == null)
            return null;

        // Attach to change tracker for updates (this will track both conversation and owned messages)
        _chatDbContext.Attach(conversation);

        return conversation;
    }

    /// <summary>
    /// Gets a conversation for a specific user, ensuring they own it.
    /// </summary>
    public async Task<Conversation?> GetConversationForUserAsync(ConversationId id, AxonUserId userId, CancellationToken ct = default)
    {
        var conversation = await GetByIdAsync(id, ct);
        
        // Use domain method to validate ownership
        if (conversation == null || !conversation.BelongsTo(userId))
            return null;
            
        return conversation;
    }

    /// <summary>
    /// Gets active conversations for a user with a limit.
    /// </summary>
    public async Task<IReadOnlyList<Conversation>> GetActiveConversationsForUserAsync(
        AxonUserId userId, 
        int limit, 
        CancellationToken ct = default)
    {
        return await _chatDbContext.Set<Conversation>()
            .AsSplitQuery()
            .Include("_messages")
            .Where(c => c.OwnerId == userId && c.Status == ConversationStatus.Active && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Checks if any conversation contains a message with the specified AI response ID.
    /// </summary>
    public async Task<bool> HasConversationWithAiResponseIdAsync(AiResponseId aiResponseId, CancellationToken ct = default)
    {
        return await _chatDbContext.Set<Message>()
            .AnyAsync(m => m.AiResponseId == aiResponseId, ct);
    }

    /// <summary>
    /// Gets a conversation with only recent messages loaded (for performance).
    /// Note: This requires raw SQL or post-processing since EF Core doesn't support filtered includes well.
    /// </summary>
    public async Task<Conversation?> GetConversationWithRecentMessagesAsync(
        ConversationId id, 
        int messageCount, 
        CancellationToken ct = default)
    {
        // First load the conversation without messages
        var conversation = await _chatDbContext.Set<Conversation>()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
            
        if (conversation == null)
            return null;

        // Then load only the recent messages
        var recentMessages = await _chatDbContext.Set<Message>()
            .Where(m => m.ConversationId == id)
            .OrderByDescending(m => m.Sequence)
            .Take(messageCount)
            .ToListAsync(ct);

        // Note: Since Messages is private, EF Core will populate it via the backing field
        // when we load the related entities with the same context
        var messagesQuery = _chatDbContext.Entry(conversation)
            .Collection("_messages")
            .Query();

        await messagesQuery
            .Cast<Message>()
            .OrderByDescending(m => m.Sequence)
            .Take(messageCount)
            .LoadAsync(ct);

        return conversation;
    }
}