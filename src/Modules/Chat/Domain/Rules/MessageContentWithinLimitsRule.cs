// /Axon/Modules/Chat/Domain/Rules/MessageContentWithinLimitsRule.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects; // Vogen MessageContent
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates that raw message content can be materialized as <see cref="MessageContent"/>.
/// Delegates all constraints to the Vogen VO.
/// </summary>
internal sealed class MessageContentWithinLimitsRule : BusinessRule
{
    private readonly string? _content;

    public MessageContentWithinLimitsRule(string? content)
        : base("Message content is invalid.", "CHAT.MESSAGE.CONTENT.INVALID")
    {
        _content = content;
    }

    public override bool IsBroken()
    {
        // Vogen exposes TryParse(string?, IFormatProvider?, out T)
        return !MessageContent.TryParse(_content, provider: null, out _);
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default)
        => ValueTask.FromResult(IsBroken());
}