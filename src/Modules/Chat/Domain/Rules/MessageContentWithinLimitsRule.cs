using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates message content before creating MessageContent value object.
/// Broken when content is null/whitespace or exceeds 100,000 characters.
/// </summary>
internal sealed class MessageContentWithinLimitsRule : BusinessRule
{
    private const int MaxContentLength = 100_000;
    private readonly string? _content;

    public MessageContentWithinLimitsRule(string? content)
        : base(
            message: DetermineMessage(content),
            code: DetermineCode(content))
    {
        _content = content;
    }

    public override bool IsBroken() 
        => string.IsNullOrWhiteSpace(_content) || _content.Length > MaxContentLength;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());

    private static string DetermineMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Message content cannot be empty.";
        
        if (content.Length > MaxContentLength)
            return $"Message content cannot exceed {MaxContentLength} characters.";
        
        return "Message content is within limits.";
    }

    private static string DetermineCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "CHAT_MESSAGE_CONTENT_EMPTY";
        
        if (content.Length > MaxContentLength)
            return "CHAT_MESSAGE_CONTENT_TOO_LONG";
        
        return "CHAT_MESSAGE_CONTENT_VALID";
    }
}