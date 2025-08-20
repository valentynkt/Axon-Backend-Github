// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ActiveByOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ActiveByOwnerSpec : Specification<Conversation>
{
    public ActiveByOwnerSpec(UserId ownerId)
    {
        Query.Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active);
    }
}