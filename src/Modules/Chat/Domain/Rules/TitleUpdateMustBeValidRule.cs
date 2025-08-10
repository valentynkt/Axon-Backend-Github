using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates title updates from users.
/// User-set titles must be 1-200 characters (trimmed).
/// </summary>
internal sealed class TitleUpdateMustBeValidRule : BusinessRule
{
    private const int MaxTitleLength = 200;
    private readonly string? _newTitle;

    public TitleUpdateMustBeValidRule(string? newTitle)
        : base(
            message: DetermineMessage(newTitle),
            code: DetermineCode(newTitle))
    {
        _newTitle = newTitle;
    }

    public override bool IsBroken()
    {
        if (string.IsNullOrWhiteSpace(_newTitle))
            return true;

        var trimmedLength = _newTitle.Trim().Length;
        return trimmedLength == 0 || trimmedLength > MaxTitleLength;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());

    private static string DetermineMessage(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Title cannot be empty.";
        
        var trimmedLength = title.Trim().Length;
        if (trimmedLength == 0)
            return "Title cannot be empty.";
        
        if (trimmedLength > MaxTitleLength)
            return "Title cannot exceed 200 characters.";
        
        return "Title is valid.";
    }

    private static string DetermineCode(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "CHAT_CONVERSATION_TITLE_EMPTY";
        
        var trimmedLength = title.Trim().Length;
        if (trimmedLength == 0)
            return "CHAT_CONVERSATION_TITLE_EMPTY";
        
        if (trimmedLength > MaxTitleLength)
            return "CHAT_CONVERSATION_TITLE_TOO_LONG";
        
        return "CHAT.CONVERSATION.TITLE_VALID";
    }
}