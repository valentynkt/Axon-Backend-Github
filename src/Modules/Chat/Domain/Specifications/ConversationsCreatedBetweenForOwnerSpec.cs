// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsCreatedBetweenForOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsCreatedBetweenForOwnerSpec : Specification<Conversation>
{
    public ConversationsCreatedBetweenForOwnerSpec(UserId ownerId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc > toUtc) (fromUtc, toUtc) = (toUtc, fromUtc);
        var from = fromUtc;
        var to   = toUtc;

        Query.Where(c =>
            c.OwnerId == ownerId &&
            c.CreatedAt >= from &&
            c.CreatedAt <= to);
    }
}