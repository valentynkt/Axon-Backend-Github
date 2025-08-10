using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Enforces a hard limit on the number of messages in a conversation.
/// Broken when the current count reaches or exceeds the maximum.
/// </summary>
internal sealed class ConversationMessageLimitRule : BusinessRule
{
    private const int DefaultMaxMessages = 10_000;
    private readonly int _currentCount;
    private readonly int _maxMessages;

    public ConversationMessageLimitRule(int currentCount, int maxMessages = DefaultMaxMessages)
        : base(
            message: $"Conversation cannot exceed {maxMessages} messages.",
            code: "CHAT_MESSAGE_LIMIT_EXCEEDED")
    {
        _currentCount = currentCount;
        _maxMessages = maxMessages;
    }

    public override bool IsBroken() => _currentCount >= _maxMessages;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}