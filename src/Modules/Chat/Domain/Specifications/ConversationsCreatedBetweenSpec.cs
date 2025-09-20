// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsCreatedBetweenSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsCreatedBetweenSpec : Specification<Conversation>
{
    public DateTimeOffset FromUtc { get; }
    public DateTimeOffset ToUtc { get; }

    public ConversationsCreatedBetweenSpec(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc > toUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        FromUtc = fromUtc;
        ToUtc = toUtc;

        // Use client-side evaluation due to SQLite DateTimeOffset limitations
        Query.AsNoTracking();
        Query.PostProcessingAction(conversations =>
            conversations.Where(c => c.CreatedAt.UtcDateTime >= fromUtc.UtcDateTime &&
                                   c.CreatedAt.UtcDateTime <= toUtc.UtcDateTime));
    }
}