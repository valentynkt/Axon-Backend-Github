using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Ensures that a conversation always has a valid owner.
/// Broken when the owner ID is default/empty.
/// </summary>
internal sealed class ConversationMustHaveOwnerRule : BusinessRule
{
    private readonly UserId _ownerId;

    public ConversationMustHaveOwnerRule(UserId ownerId)
        : base(
            message: "Conversation owner must be specified.",
            code: "CHAT.CONVERSATION.OWNER.REQUIRED")
    {
        _ownerId = ownerId;
    }

    public override bool IsBroken() => _ownerId == null || _ownerId.Value == Guid.Empty;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}