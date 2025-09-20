// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/RecentlyUpdatedByOwnerSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class RecentlyUpdatedByOwnerSpec : Specification<Conversation>
{
    public AxonUserId OwnerId { get; }
    public DateTimeOffset SinceUtc { get; }

    public RecentlyUpdatedByOwnerSpec(AxonUserId ownerId, DateTimeOffset sinceUtc)
    {
        OwnerId = ownerId;
        SinceUtc = sinceUtc;

        // Filter by owner and status first (this works fine in SQLite)
        Query.Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active);

        // Use PostProcessingAction for date filtering since SQLite has issues with DateTimeOffset
        Query.PostProcessingAction(conversations => conversations
            .Where(c =>
            {
                var lastUpdatedUtc = c.UpdatedAt.HasValue
                    ? c.UpdatedAt.Value.UtcDateTime
                    : c.CreatedAt.UtcDateTime;
                return lastUpdatedUtc >= sinceUtc.UtcDateTime;
            })
            .ToList());
    }
}