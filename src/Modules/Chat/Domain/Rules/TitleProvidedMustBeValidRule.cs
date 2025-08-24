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
        // Null title is allowed on Start (will become null in domain)
        if (_title is null)
            return false;

        // Empty/whitespace title is allowed on Start (will become null in domain)
        if (string.IsNullOrWhiteSpace(_title))
            return false;

        // If title is provided and not empty/whitespace, it must be valid according to VO rules
        try
        {
            var result = ConversationTitle.Create(_title);
            return result.IsFailure;
        }
        catch (Exception)
        {
            // If ConversationTitle.Create throws an exception, treat as invalid
            return true;
        }
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}