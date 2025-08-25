using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Specifications.Messages;

/// <summary>
/// Specification for counting messages in a specific conversation.
/// Used for pagination calculations in GetConversationMessages query.
/// </summary>
public sealed class MessagesForConversationCountSpec : CountSpecification<Message>
{
    public MessagesForConversationCountSpec(
        ConversationId conversationId,
        bool includeDeleted = false)
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
    }
}