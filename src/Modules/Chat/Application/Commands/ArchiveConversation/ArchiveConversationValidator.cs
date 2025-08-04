using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.ArchiveConversation;

/// <summary>
/// Validator for archive conversation command
/// </summary>
public sealed class ArchiveConversationValidator : AbstractValidator<ArchiveConversationCommand>
{
    public ArchiveConversationValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("ConversationId is required");
    }
}