using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;
using Axon.Modules.Chat.Application.Validation.Extensions;

namespace Axon.Modules.Chat.Application.Commands.UpdateConversationTitle;

/// <summary>
/// Application layer validation for UpdateConversationTitle command.
/// Uses domain validation as single source of truth to eliminate duplication.
/// </summary>
public sealed class UpdateConversationTitleValidator : BaseValidator<UpdateConversationTitleCommand>
{
    public UpdateConversationTitleValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmptyGuid()
            .WithErrorCode(ValidationErrorCodes.GuidEmpty);

        RuleFor(x => x.Title)
            .NotNull()
            .WithErrorCode(ValidationErrorCodes.Required)
            .MustBeValidConversationTitle();
    }
}