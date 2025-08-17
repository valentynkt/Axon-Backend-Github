using BuildingBlocks.Application.Validation.Base;
using BuildingBlocks.Application.Validation.Extensions;
using BuildingBlocks.Application.Validation.Constants;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Application layer validation for GetConversation query.
/// 
/// Separation of Concerns:
/// - Application Layer (this validator): Input format validation (GUID not empty)
/// - Handler Layer: Authorization, existence checks, business logic
/// 
/// Simple input validation for query parameters.
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