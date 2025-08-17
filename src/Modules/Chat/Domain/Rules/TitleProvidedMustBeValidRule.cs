using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates optional title provided when starting a conversation.
/// For Start operation: empty title is allowed, but if provided must be valid.
/// Delegates to ConversationTitle value object for consistent validation.
/// </summary>
internal sealed class TitleProvidedMustBeValidRule : BusinessRule
{
    private readonly string? _title;

    public TitleProvidedMustBeValidRule(string? title)
        : base(
            message: "Provided title is invalid.",
            code: "CHAT.CONVERSATION.TITLE.INVALID")
    {
        _title = title;
    }

    public override bool IsBroken()
    {
        // Empty/null title is allowed on Start
        if (string.IsNullOrEmpty(_title))
            return false;

        // If title is provided, it must be valid according to VO rules
        var result = ConversationTitle.Create(_title);
        return result.IsFailure;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}