// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsUpdatedSinceSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsUpdatedSinceSpec : Specification<Conversation>
{
    public ConversationsUpdatedSinceSpec(DateTimeOffset sinceUtc)
    {
        var since = sinceUtc;
        Query.Where(c => (c.UpdatedAt ?? c.CreatedAt) >= since);
    }
}