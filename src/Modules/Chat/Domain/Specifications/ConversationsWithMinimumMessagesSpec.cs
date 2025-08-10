using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsWithMinimumMessagesSpec : Specification<Conversation>
{
    private readonly int _minCount;

    public ConversationsWithMinimumMessagesSpec(int minCount)
    {
        // Clamp negative values to 0
        _minCount = Math.Max(0, minCount);
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
        => c => c.MessageCount >= _minCount;
}