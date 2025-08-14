using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsUpdatedSinceSpec : Specification<Conversation>
{
    private readonly DateTimeOffset _sinceUtc;

    public ConversationsUpdatedSinceSpec(DateTimeOffset sinceUtc)
    {
        _sinceUtc = sinceUtc;
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
        => c => c.UpdatedAt >= _sinceUtc;
}