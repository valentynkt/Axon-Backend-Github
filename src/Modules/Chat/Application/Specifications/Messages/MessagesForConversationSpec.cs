using Axon.Modules.Chat.Application.Common.Pagination;
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
        // Build the complete query in a single chain like ConversationsForOwnerSpec
        var query = ConfigureQuery()
            .Where(m => m.ConversationId == conversationId);

        // Apply deleted filter conditionally
        if (!includeDeleted)
        {
            query = query.Where(m => !m.IsDeleted);
        }

        // Apply ordering and projection in the same chain
        query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new ConversationMessageItem(
                m.Id.Value,
                m.Role.Value, // Convert MessageRole value object to string
                m.Content.Value, // Convert MessageContent value object to string
                m.CreatedAt.DateTime,
                m.Sequence
            ));
    }
}