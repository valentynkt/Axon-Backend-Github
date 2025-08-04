using FluentValidation;

namespace Axon.Modules.Chat.Application.Queries.SearchConversations;

/// <summary>
/// Validator for SearchConversationsQuery
/// </summary>
public sealed class SearchConversationsValidator : AbstractValidator<SearchConversationsQuery>
{
    public SearchConversationsValidator()
    {
        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip must be greater than or equal to 0");

        RuleFor(x => x.Take)
            .GreaterThan(0)
            .WithMessage("Take must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Take cannot exceed 100");

        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrEmpty(status) || 
                           status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                           status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                           status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: Active, Completed, Archived");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(200)
            .WithMessage("Search term cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.SearchTerm));

        RuleFor(x => x.CreatedBefore)
            .GreaterThan(x => x.CreatedAfter)
            .WithMessage("CreatedBefore must be after CreatedAfter")
            .When(x => x.CreatedAfter.HasValue && x.CreatedBefore.HasValue);
    }
}