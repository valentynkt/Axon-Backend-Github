// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsByOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsByOwnerSpec : Specification<Conversation>
{
    public ConversationsByOwnerSpec(AxonUserId ownerId)
    {
        Query.Where(c => c.OwnerId == ownerId);
    }
}