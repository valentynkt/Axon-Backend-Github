// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsCreatedBetweenSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsCreatedBetweenSpec : Specification<Conversation>
{
    public ConversationsCreatedBetweenSpec(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc > toUtc) (fromUtc, toUtc) = (toUtc, fromUtc);
        var from = fromUtc;
        var to   = toUtc;

        Query.Where(c => c.CreatedAt >= from && c.CreatedAt <= to);
    }
}