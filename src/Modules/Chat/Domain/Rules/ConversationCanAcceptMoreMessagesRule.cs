using Axon.Modules.Chat.Primitives.Constants;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Rule to validate that a conversation can accept more messages.
/// This provides more explicit naming than the generic ConversationMessageLimitRule.
/// </summary>
internal sealed class ConversationCanAcceptMoreMessagesRule : BusinessRule
{
    private const int DefaultMaxMessages = ChatPrimitiveConstants.Conversation.MaxMessages;
    private readonly int _currentCount;
    private readonly int _maxMessages;

    public ConversationCanAcceptMoreMessagesRule(int currentCount, int maxMessages = DefaultMaxMessages)
        : base(
            message: $"Conversation has reached its maximum capacity of {maxMessages} messages and cannot accept more.",
            code: "CHAT.CONVERSATION.MESSAGE.CAPACITY.EXCEEDED")
    {
        _currentCount = currentCount;
        _maxMessages = maxMessages;
    }

    public override bool IsBroken() => _currentCount >= _maxMessages;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}