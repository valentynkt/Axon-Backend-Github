// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsWithMinimumMessagesSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsWithMinimumMessagesSpec : Specification<Conversation>
{
    public ConversationsWithMinimumMessagesSpec(int minCount)
    {
        var min = Math.Max(0, minCount);
        // Uses the aggregate's computed MessageCount; ensure your infra can translate or map accordingly.
        Query.Where(c => c.MessageCount >= min);
    }
}