using FluentValidation;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

public sealed class StartConversationValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        // No validation needed - Domain handles title validation through TitleProvidedMustBeValidRule
        // Title can be null/empty for default titles
    }
}