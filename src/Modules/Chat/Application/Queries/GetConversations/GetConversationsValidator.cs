using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Validator for GetConversationsQuery ensuring all parameters are within valid ranges.
/// </summary>
public sealed class GetConversationsValidator : AbstractValidator<GetConversationsQuery>
{
    public GetConversationsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Page.MaxSize)
            .WithMessage($"Page size must be between 1 and {Page.MaxSize}.");

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