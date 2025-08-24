using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that ensures a conversation exists.
/// Note: This rule requires the actual conversation object to be passed,
/// as domain rules cannot directly access repositories.
/// </summary>
public sealed class ConversationMustExistRule : BusinessRule
{
    private readonly Conversation? _conversation;
    private readonly ConversationId _conversationId;

    public ConversationMustExistRule(Conversation? conversation, ConversationId conversationId)
        : base(
            message: $"Conversation {conversationId.Value} not found.",
            code: ChatDomainErrors.Conversation.NotFoundCode)
    {
        _conversation = conversation;
        _conversationId = conversationId;
    }

    public override bool IsBroken() => _conversation is null;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}