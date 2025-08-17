using BuildingBlocks.Application.Validation.Base;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

public sealed class StartConversationValidator : BaseValidator<StartConversationCommand>
{
    public StartConversationValidator()
    {
        // No input validation needed for StartConversation
        // - Title is optional (can be null for auto-generated titles)
        // - Domain handles business validation through TitleProvidedMustBeValidRule
        // - User context validation handled in pipeline
    }
}