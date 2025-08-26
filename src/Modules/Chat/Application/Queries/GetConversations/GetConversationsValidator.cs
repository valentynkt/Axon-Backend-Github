using Axon.Modules.Chat.Application.Common.Validation;
using FluentValidation;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Validator for GetConversationsQuery. 
/// Pagination validation is handled by BasePaginatedChatValidator.
/// </summary>
public sealed class GetConversationsValidator : BasePaginatedChatValidator<GetConversationsQuery>
{
    public GetConversationsValidator()
    {
        RuleFor(x => x.SortBy)
            .IsInEnum()
            .WithMessage("Sort by must be a valid conversation sort field.");

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("Sort direction must be either Asc or Desc.");

        RuleFor(x => x.TitleContains)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.TitleContains))
            .WithMessage("Title filter must be 100 characters or less.");
    }
}