// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Domain/Specifications/ConversationsWithMinimumMessagesSpec.cs
#nullable enable
using Ardalis.Specification;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Domain.Specifications;

public sealed class ConversationsWithMinimumMessagesSpec : Specification<Conversation>
{
    public ConversationsWithMinimumMessagesSpec(int minCount)
    {
        var min = Math.Max(0, minCount);
        // Use EF.Property to access the private backing field
        Query.Where(c => EF.Property<List<Message>>(c, "_messages").Count >= min);
    }
}