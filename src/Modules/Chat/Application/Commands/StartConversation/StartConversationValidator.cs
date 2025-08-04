using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Validator for StartConversationCommand
/// </summary>
public sealed class StartConversationValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Conversation title is required")
            .MaximumLength(200)
            .WithMessage("Conversation title cannot exceed 200 characters");
    }
}