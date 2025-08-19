using BuildingBlocks.Application.Validation.Base;
using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Lean validator: Message is required (VO handles detailed validation).
/// </summary>
public sealed class StartConversationValidator : BaseValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        RuleFor(x => x.Message)
            .NotNull()
            .WithMessage("Message is required.");
    }
}