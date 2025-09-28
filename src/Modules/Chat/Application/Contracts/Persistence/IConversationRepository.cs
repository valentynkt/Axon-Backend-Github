using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Contracts.Persistence;

/// <summary>
/// Repository for Conversation aggregate persistence
/// </summary>
public interface IConversationRepository : IWriteRepository<Conversation, ConversationId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management
    /// </summary>
    IWriteUnitOfWork<ChatModule> UnitOfWork { get; }

    /// <summary>
    /// Gets a conversation for a specific user, ensuring they own it.
    /// </summary>
    Task<Conversation?> GetConversationForUserAsync(ConversationId id, AxonUserId userId, CancellationToken ct = default);

    /// <summary>
    /// Gets active conversations for a user with a limit.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetActiveConversationsForUserAsync(AxonUserId userId, int limit, CancellationToken ct = default);

    /// <summary>
    /// Checks if any conversation contains a message with the specified AI response ID.
    /// </summary>
    Task<bool> HasConversationWithAiResponseIdAsync(AiResponseId aiResponseId, CancellationToken ct = default);

    /// <summary>
    /// Gets a conversation with only recent messages loaded (for performance).
    /// </summary>
    Task<Conversation?> GetConversationWithRecentMessagesAsync(ConversationId id, int messageCount, CancellationToken ct = default);
}