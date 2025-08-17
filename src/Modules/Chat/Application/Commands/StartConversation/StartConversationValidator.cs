namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Validator for StartConversationCommand
/// </summary>
public sealed class StartConversationValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => t == null || t.Trim().Length <= 200)
            .WithErrorCode("CHAT.CONVERSATION.TITLE.TOO_LONG")
            .WithMessage("Title cannot exceed 200 characters.");
    }
}