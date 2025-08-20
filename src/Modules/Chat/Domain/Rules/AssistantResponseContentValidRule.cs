using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Rule to validate that assistant response content meets specific standards.
/// </summary>
internal sealed class AssistantResponseContentValidRule : BusinessRule
{
    private readonly string _content;

    public AssistantResponseContentValidRule(string content)
        : base(
            message: "Assistant response content does not meet validation standards.",
            code: "CHAT.MESSAGE.ASSISTANT.CONTENT.INVALID")
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public override bool IsBroken()
    {
        // Assistant responses must have substantive content
        if (string.IsNullOrWhiteSpace(_content))
            return true;

        // Assistant responses should have meaningful length (at least 1 character after trimming)
        var trimmedContent = _content.Trim();
        if (trimmedContent.Length < 1)
            return true;

        // Check for excessive control characters (excluding normal whitespace)
        var controlChars = _content.Count(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t');
        if (controlChars > 5) // Allow some formatting control chars but not excessive
            return true;

        // Content is valid
        return false;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken cancellationToken = default) 
        => ValueTask.FromResult(IsBroken());
}