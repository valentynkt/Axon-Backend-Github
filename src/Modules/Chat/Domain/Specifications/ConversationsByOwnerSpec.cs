using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Specifications;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Specifications;

internal sealed class ConversationsByOwnerSpec : Specification<Conversation>
{
    private readonly UserId _ownerId;

    public ConversationsByOwnerSpec(UserId ownerId)
    {
        _ownerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
    }

    public override Expression<Func<Conversation, bool>> ToExpression()
        => c => c.OwnerId == _ownerId;
}