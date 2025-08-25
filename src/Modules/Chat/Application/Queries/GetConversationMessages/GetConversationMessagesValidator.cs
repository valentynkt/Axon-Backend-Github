namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Validator for GetConversationMessagesQuery ensuring all parameters are within valid ranges.
/// </summary>
public sealed class GetConversationMessagesValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Page.MaxSize)
            .WithMessage($"Page size must be between 1 and {Page.MaxSize}.");
    }
}