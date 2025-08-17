using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

public sealed class AppendUserMessageValidator : BaseValidator<AppendUserMessageCommand>
{
    public AppendUserMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmptyGuid()
            .WithErrorCode(ValidationErrorCodes.GuidEmpty);

        RuleFor(x => x.Content)
            .NotEmptyOrWhitespace()
            .WithErrorCode(ValidationErrorCodes.ContentEmpty);

        // Note: Domain-specific content validation is handled by MessageContent.Create()
        // in the domain layer for proper separation of concerns
    }
}