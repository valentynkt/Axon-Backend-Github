using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Rule to validate that message content meets domain-specific standards.
/// </summary>
internal sealed class MessageContentMeetsDomainStandardsRule : BusinessRule
{
    private readonly string _content;

    public MessageContentMeetsDomainStandardsRule(string content)
        : base(
            message: "Message content does not meet domain standards for user messages.",
            code: "CHAT.MESSAGE.CONTENT.DOMAIN.STANDARDS.VIOLATION")
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public override bool IsBroken()
    {
        // Enhanced validation: Check for potentially problematic content patterns
        if (string.IsNullOrWhiteSpace(_content))
            return true;

        // Check for excessive control characters (excluding normal whitespace)
        var controlChars = _content.Count(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t');
        if (controlChars > 0)
            return true;

        // Content meets domain standards
        return false;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}