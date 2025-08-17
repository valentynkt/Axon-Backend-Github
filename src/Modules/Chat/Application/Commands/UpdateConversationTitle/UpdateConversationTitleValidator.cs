namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Validator for UpdateConversationTitleCommand
/// </summary>
public sealed class UpdateConversationTitleValidator : AbstractValidator<UpdateConversationTitleCommand>
{
    private const int MinTitleLength = 1;
    private const int MaxTitleLength = 200;

    public UpdateConversationTitleValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithErrorCode("CHAT.CONVERSATION.ID.EMPTY")
            .WithMessage("Conversation ID is required.");

        RuleFor(x => x.Title)
            .NotNull()
            .WithErrorCode("CHAT.CONVERSATION.TITLE.REQUIRED")
            .WithMessage("Title is required.")
            .Must(t => t != null && t.Trim().Length >= MinTitleLength && t.Trim().Length <= MaxTitleLength)
            .WithErrorCode("CHAT.CONVERSATION.TITLE.INVALID")
            .WithMessage($"Title must be {MinTitleLength}-{MaxTitleLength} characters after trimming.");
    }
}