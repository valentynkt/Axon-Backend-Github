using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that validates message content is within acceptable limits.
/// Delegates to MessageContent value object for consistent validation.
/// </summary>
internal sealed class MessageContentWithinLimitsRule : BusinessRule
{
    private readonly string? _content;

    public MessageContentWithinLimitsRule(string? content)
        : base(
            message: "Message content is invalid.",
            code: "CHAT.MESSAGE.CONTENT.INVALID")
    {
        _content = content;
    }

    public override bool IsBroken()
    {
        var result = MessageContent.Create(_content);
        return result.IsFailure;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}