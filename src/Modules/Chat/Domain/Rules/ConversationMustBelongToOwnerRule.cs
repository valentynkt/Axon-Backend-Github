using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures a conversation belongs to the specified owner.
/// </summary>
internal sealed class ConversationMustBelongToOwnerRule : BusinessRule
{
    private readonly Conversation _conversation;
    private readonly AxonUserId _expectedOwnerId;

    public ConversationMustBelongToOwnerRule(Conversation conversation, AxonUserId expectedOwnerId)
        : base(
            message: "Conversation does not belong to the current user.",
            code: "CHAT.CONVERSATION.ACCESS_DENIED")
    {
        _conversation = conversation;
        _expectedOwnerId = expectedOwnerId;
    }

    public override bool IsBroken() => !_conversation.BelongsTo(_expectedOwnerId);

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}