using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsByStatusSpec : Specification<Conversation>
{
    private readonly ConversationStatus _status;

    public ConversationsByStatusSpec(ConversationStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
        => c => c.Status == _status;
}