using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures a ConversationId is valid (not empty).
/// </summary>
internal sealed class ConversationIdMustBeValidRule : BusinessRule
{
    private readonly ConversationId _conversationId;

    public ConversationIdMustBeValidRule(ConversationId conversationId)
        : base(
            message: "ConversationId cannot be empty.",
            code: "CHAT.ID.EMPTY")
    {
        _conversationId = conversationId;
    }

    public override bool IsBroken() => _conversationId.Value == Guid.Empty;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}