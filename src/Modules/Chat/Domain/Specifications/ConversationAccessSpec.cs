using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Specification to verify that a specific conversation exists and is owned by a user.
/// Core domain specification for access control validation.
/// </summary>
public sealed class ConversationAccessSpec : Specification<Conversation>
{
    public ConversationAccessSpec(ConversationId conversationId, AxonUserId ownerId)
    {
        Query
            .Where(c => c.Id == conversationId && c.OwnerId == ownerId)
            .AsNoTracking();
    }
}