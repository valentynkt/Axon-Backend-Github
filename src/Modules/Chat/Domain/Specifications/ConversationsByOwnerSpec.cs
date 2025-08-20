// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsByOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsByOwnerSpec : Specification<Conversation>
{
    public ConversationsByOwnerSpec(UserId ownerId)
    {
        Query.Where(c => c.OwnerId == ownerId);
    }
}