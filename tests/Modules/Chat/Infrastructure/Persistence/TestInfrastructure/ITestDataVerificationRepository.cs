using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Repository interface for test-specific data verification queries.
/// Provides clean abstraction over database queries used in tests,
/// eliminating the need for direct SQL queries in test code.
/// </summary>
public interface ITestDataVerificationRepository
{
    /// <summary>
    /// Gets the sequence number for a specific message.
    /// </summary>
    Task<int?> GetMessageSequenceAsync(ConversationId conversationId, MessageId messageId, CancellationToken ct = default);

    /// <summary>
    /// Gets all message sequences for a conversation in order.
    /// </summary>
    Task<List<MessageSequenceInfo>> GetMessageSequencesAsync(ConversationId conversationId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a message is soft-deleted.
    /// </summary>
    Task<bool> IsMessageDeletedAsync(MessageId messageId, CancellationToken ct = default);

    /// <summary>
    /// Gets the xmin version for optimistic concurrency control.
    /// </summary>
    Task<uint> GetConversationVersionAsync(ConversationId conversationId, CancellationToken ct = default);

    /// <summary>
    /// Gets the count of messages in a conversation.
    /// </summary>
    Task<int> GetMessageCountAsync(ConversationId conversationId, CancellationToken ct = default);

    /// <summary>
    /// Gets soft-deleted message information.
    /// </summary>
    Task<DeletedMessageInfo?> GetDeletedMessageInfoAsync(ConversationId conversationId, MessageId messageId, CancellationToken ct = default);

    /// <summary>
    /// Checks if an AI response ID exists globally across all conversations.
    /// </summary>
    Task<bool> AiResponseIdExistsAsync(AiResponseId aiResponseId, CancellationToken ct = default);

    /// <summary>
    /// Gets messages by role for testing role distribution.
    /// </summary>
    Task<List<Message>> GetMessagesByRoleAsync(ConversationId conversationId, MessageRole role, CancellationToken ct = default);
}

/// <summary>
/// Information about message sequences for verification.
/// </summary>
public sealed record MessageSequenceInfo(int Sequence, MessageId Id);

/// <summary>
/// Information about soft-deleted messages.
/// </summary>
public sealed record DeletedMessageInfo(MessageId Id, bool IsDeleted);