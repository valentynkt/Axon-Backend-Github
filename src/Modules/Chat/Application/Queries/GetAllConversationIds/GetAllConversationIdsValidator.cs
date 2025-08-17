namespace Axon.Modules.Chat.Application.Queries.GetAllConversationIds;

/// <summary>
/// Validator for GetAllConversationIdsQuery
/// </summary>
public sealed class GetAllConversationIdsValidator : AbstractValidator<GetAllConversationIdsQuery>
{
    public GetAllConversationIdsValidator()
    {
        // Intentionally empty - authentication handled in handler pipeline
    }
}