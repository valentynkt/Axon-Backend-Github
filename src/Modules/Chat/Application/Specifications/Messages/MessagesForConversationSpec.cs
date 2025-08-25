using Axon.Modules.Chat.Application.Queries.GetConversationMessages;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Specifications.Messages;

/// <summary>
/// Specification for retrieving paginated messages for a specific conversation.
/// Orders messages chronologically by sequence number for natural conversation flow.
/// Projects to ConversationMessageItem for optimized data transfer.
/// </summary>
public sealed class MessagesForConversationSpec : PagedSpecification<Message, ConversationMessageItem>
{
    public MessagesForConversationSpec(
        ConversationId conversationId,
        Page page,
        bool includeDeleted = false)
        : base(page)
    {
        // Filter by conversation
        ConfigureQuery()
            .Where(m => m.ConversationId == conversationId);

        // Filter deleted messages if not requested
        if (!includeDeleted)
        {
            ConfigureQuery()
                .Where(m => !m.IsDeleted);
        }

        // Order chronologically by sequence (ascending for natural flow)
        // Use MessageId as tie-breaker for deterministic pagination
        ConfigureQuery()
            .OrderBy(m => m.Sequence)
            .ThenBy(m => m.Id.Value);

        // Project to ConversationMessageItem for optimized data transfer
        ConfigureQuery()
            .Select(m => new ConversationMessageItem(
                m.Id.Value,
                m.Role.Value, // Convert MessageRole value object to string
                m.Content.Value, // Convert MessageContent value object to string
                m.CreatedAt.DateTime,
                m.Sequence
            ));

    }
}