using BuildingBlocks.Application.Validation.Base;

namespace Axon.Modules.Chat.Application.Queries.GetAllConversationIds;

/// <summary>
/// Validator for GetAllConversationIdsQuery
/// </summary>
public sealed class GetAllConversationIdsValidator : BaseValidator<GetAllConversationIdsQuery>
{
    public GetAllConversationIdsValidator()
    {
        // No input validation needed - query has no parameters
        // Authentication and authorization handled in pipeline
    }
}