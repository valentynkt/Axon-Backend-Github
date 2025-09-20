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

        // SQLite DateTimeOffset translation is handled by repository override
        // This specification is processed client-side for SQLite compatibility
    }
}