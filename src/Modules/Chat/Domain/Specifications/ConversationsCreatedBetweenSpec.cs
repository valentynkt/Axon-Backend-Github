using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsCreatedBetweenSpec : Specification<Conversation>
{
    private readonly DateTimeOffset _fromUtc;
    private readonly DateTimeOffset _toUtc;

    public ConversationsCreatedBetweenSpec(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        // Normalize: if fromUtc > toUtc, swap for ergonomics
        if (fromUtc > toUtc)
        {
            (_fromUtc, _toUtc) = (toUtc, fromUtc);
        }
        else
        {
            _fromUtc = fromUtc;
            _toUtc = toUtc;
        }
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
        => c => c.CreatedAtUtc >= _fromUtc && c.CreatedAtUtc <= _toUtc;
}