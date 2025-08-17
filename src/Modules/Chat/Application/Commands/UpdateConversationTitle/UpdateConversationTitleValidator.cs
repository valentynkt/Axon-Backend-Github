using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

public sealed class UpdateConversationTitleValidator : BaseValidator<UpdateConversationTitleCommand>
{
    public UpdateConversationTitleValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmptyGuid()
            .WithErrorCode(ValidationErrorCodes.GuidEmpty);

        RuleFor(x => x.Title)
            .NotEmptyOrWhitespace()
            .WithErrorCode(ValidationErrorCodes.StringEmptyOrWhitespace);

        // Note: Domain-specific title validation (length, format, etc.) is handled by 
        // ConversationTitle.Create() and TitleUpdateMustBeValidRule in the domain layer
    }
}