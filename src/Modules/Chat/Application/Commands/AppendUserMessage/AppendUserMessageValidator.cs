using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;
using Axon.Modules.Chat.Application.Validation.Extensions;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Application layer validation for AppendUserMessage command.
/// Uses domain validation as single source of truth to eliminate duplication.
/// </summary>
public sealed class AppendUserMessageValidator : BaseValidator<AppendUserMessageCommand>
{
    public AppendUserMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmptyGuid()
            .WithErrorCode(ValidationErrorCodes.GuidEmpty);

        RuleFor(x => x.Content)
            .MustBeValidMessageContent();

        RuleFor(x => x.IdempotencyKey)
            .ContentLength(1, 256) // When provided, should be reasonable length
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey))
            .WithErrorCode(ValidationErrorCodes.StringInvalidLength);
    }
}