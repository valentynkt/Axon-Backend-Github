// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsByStatusSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsByStatusSpec : Specification<Conversation>
{
    public ConversationsByStatusSpec(ConversationStatus status)
    {
        Query.Where(c => c.Status == status);
    }
}