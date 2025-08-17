using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Ensures that a conversation has at least one message before it can be completed.
/// Broken when attempting to complete an empty conversation.
/// </summary>
internal sealed class CompletionRequiresAtLeastOneMessageRule : BusinessRule
{
    private readonly int _messageCount;

    public CompletionRequiresAtLeastOneMessageRule(int messageCount)
        : base(
            message: "Cannot complete an empty conversation.",
            code: "CHAT.CONVERSATION.EMPTY.ON.COMPLETE",
            metadata: new Dictionary<string, object> { ["messageCount"] = messageCount })
    {
        _messageCount = messageCount;
    }

    public override bool IsBroken() => _messageCount == 0;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}