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
        // Use backing field to count messages since EF Core can translate this
        Query.Where(c => EF.Property<List<Message>>(c, "_messages").Count >= min);
    }
}