using Axon.Modules.Chat.Application.Common.Validation;
using FluentValidation;

namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Validator for GetConversationMessagesQuery.
/// Pagination validation is handled by BasePaginatedChatValidator.
/// </summary>
public sealed class GetConversationMessagesValidator : BasePaginatedChatValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEqual(Guid.Empty)
            .WithMessage("Conversation ID is required.");
    }
}