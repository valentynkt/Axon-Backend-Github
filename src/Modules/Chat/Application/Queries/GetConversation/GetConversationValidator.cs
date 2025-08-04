using FluentValidation;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Validator for GetConversationQuery
/// </summary>
public sealed class GetConversationValidator : AbstractValidator<GetConversationQuery>
{
    public GetConversationValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required");
    }
}