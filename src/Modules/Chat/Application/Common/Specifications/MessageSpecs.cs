// /Modules/Chat/Application/Common/Specifications/MessageSpecs.cs
#nullable enable
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using Axon.Modules.Chat.Application.Specifications.Messages;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Common.Specifications;

/// <summary>
/// Fluent builder for message-related specifications.
/// Provides a clean, reusable API for building message queries with proper encapsulation.
/// </summary>
public static class MessageSpecs
{
    /// <summary>
    /// Creates a specification for retrieving paginated messages in a specific conversation.
    /// Messages are ordered chronologically by creation time and sequence.
    /// </summary>
    /// <param name="conversationId">The conversation ID to retrieve messages for</param>
    /// <param name="page">Pagination configuration</param>
    /// <param name="includeDeleted">Whether to include soft-deleted messages (default: false)</param>
    /// <returns>Paged specification for messages in the conversation</returns>
    public static ISpecification<Message, ConversationMessageItem> ForConversation(
        ConversationId conversationId,
        Page page,
        bool includeDeleted = false)
    {
        return new MessagesForConversationSpec(conversationId, page, includeDeleted);
    }

    /// <summary>
    /// Creates a specification for counting messages in a specific conversation.
    /// This is optimized for count operations and doesn't project to full message data.
    /// </summary>
    /// <param name="conversationId">The conversation ID to count messages for</param>
    /// <param name="includeDeleted">Whether to include soft-deleted messages in count (default: false)</param>
    /// <returns>Count specification for messages in the conversation</returns>
    public static ISpecification<Message> ForConversationCount(
        ConversationId conversationId,
        bool includeDeleted = false)
    {
        return new MessageCountSpec(conversationId, includeDeleted);
    }

    /// <summary>
    /// Simple specification for counting messages in a conversation.
    /// Used for pagination total count calculations without loading message data.
    /// </summary>
    private sealed class MessageCountSpec : Specification<Message>
    {
        public MessageCountSpec(ConversationId conversationId, bool includeDeleted)
        {
            var query = Query
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId);

            if (!includeDeleted)
            {
                query.Where(m => !m.IsDeleted);
            }
        }
    }
}