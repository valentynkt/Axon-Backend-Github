using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Validator for ProcessMessageCommand
/// </summary>
public sealed class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    private const int MaxMessageLength = 4000;

    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MaximumLength(MaxMessageLength)
            .WithMessage($"Message cannot exceed {MaxMessageLength} characters");
    }
}