// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/RecentlyUpdatedByOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class RecentlyUpdatedByOwnerSpec : Specification<Conversation>
{
    public RecentlyUpdatedByOwnerSpec(UserId ownerId, DateTimeOffset sinceUtc)
    {
        var since = sinceUtc;
        Query.Where(c =>
            c.OwnerId == ownerId &&
            c.Status == ConversationStatus.Active &&
            (c.UpdatedAt ?? c.CreatedAt) >= since);
    }
}