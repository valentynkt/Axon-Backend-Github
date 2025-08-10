using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates optional title provided when starting a conversation.
/// For Start operation: empty title is allowed, but if provided must be ≤ 200 chars.
/// </summary>
internal sealed class TitleProvidedMustBeValidRule : BusinessRule
{
    private const int MaxTitleLength = 200;
    private readonly string? _title;

    public TitleProvidedMustBeValidRule(string? title)
        : base(
            message: "Title cannot exceed 200 characters.",
            code: "CHAT_CONVERSATION_TITLE_TOO_LONG",
            metadata: new Dictionary<string, object> { ["max"] = MaxTitleLength, ["actual"] = title?.Length ?? 0 })
    {
        _title = title;
    }

    public override bool IsBroken()
    {
        // Empty/null title is allowed on Start
        if (string.IsNullOrEmpty(_title))
            return false;

        // If title is provided, it must not exceed the limit
        return _title.Length > MaxTitleLength;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}