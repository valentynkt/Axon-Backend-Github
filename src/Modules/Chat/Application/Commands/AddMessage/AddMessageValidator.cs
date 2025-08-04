using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.AddMessage;

/// <summary>
/// Validator for AddMessageCommand
/// </summary>
public sealed class AddMessageValidator : AbstractValidator<AddMessageCommand>
{
    public AddMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content is required")
            .MaximumLength(100_000)
            .WithMessage("Message content cannot exceed 100,000 characters");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Message role is required")
            .Must(role => role is "user" or "assistant" or "system" or "tool")
            .WithMessage("Message role must be one of: user, assistant, system, tool");
    }
}