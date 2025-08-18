using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Validator for ProcessMessageCommand
/// </summary>
public sealed class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(4000).WithMessage("Message must not exceed 4000 characters");
    }
}