using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Ensures that operations can only be performed on active conversations.
/// Broken when status is not Active.
/// </summary>
internal sealed class ConversationMustBeActiveRule : BusinessRule
{
    private readonly ConversationStatus _status;

    public ConversationMustBeActiveRule(ConversationStatus status)
        : base(
            message: "Conversation must be active to perform this operation.",
            code: "CHAT_CONVERSATION_NOT_ACTIVE")
    {
        _status = status;
    }

    public override bool IsBroken() => _status != ConversationStatus.Active;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}