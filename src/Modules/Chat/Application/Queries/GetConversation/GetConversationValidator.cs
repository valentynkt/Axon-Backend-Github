using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Validator for GetConversationQuery
/// </summary>
public sealed class GetConversationValidator : BaseValidator<GetConversationQuery>
{
    public GetConversationValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmptyGuid()
            .WithErrorCode(ValidationErrorCodes.GuidEmpty);
    }
}