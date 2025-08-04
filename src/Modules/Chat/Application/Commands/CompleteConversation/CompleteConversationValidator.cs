using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.CompleteConversation;

/// <summary>
/// Validator for CompleteConversationCommand
/// </summary>
public sealed class CompleteConversationValidator : AbstractValidator<CompleteConversationCommand>
{
    public CompleteConversationValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required");
    }
}