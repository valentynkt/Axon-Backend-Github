using Axon.Api.Constants;
using Axon.Api.Contracts.V1.Chat;
using FastEndpoints;
using FluentValidation;

namespace Axon.Api.Endpoints.V1.Chat.Queries.GetConversations;

/// <summary>
/// Validator for GetConversations request to ensure proper sort field values
/// </summary>
public sealed class GetConversationsRequestValidator : Validator<GetConversationsRequestDto>
{
    public GetConversationsRequestValidator()
    {
        // Validate SortBy field - must be null or one of the allowed values
        RuleFor(x => x.SortBy)
            .Must(sortBy => sortBy is null || ChatApiConstants.SortBy.ValidValues.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ChatApiConstants.SortBy.ValidValues)}. Received: '{{PropertyValue}}'")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));

        // Validate SortDirection field - must be null or one of the allowed values
        RuleFor(x => x.SortDirection)
            .Must(sortDirection => sortDirection is null || ChatApiConstants.SortDirection.ValidValues.Contains(sortDirection, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortDirection must be one of: {string.Join(", ", ChatApiConstants.SortDirection.ValidValues)}. Received: '{{PropertyValue}}'")
            .When(x => !string.IsNullOrWhiteSpace(x.SortDirection));

        // Validate TitleContains - if provided, should not be empty or whitespace only
        RuleFor(x => x.TitleContains)
            .Must(title => title is null || !string.IsNullOrWhiteSpace(title))
            .WithMessage("TitleContains, when provided, cannot be empty or whitespace only")
            .When(x => x.TitleContains is not null);

        // Inherit pagination validation from base class
        Include(new BasePagedRequestValidator());
    }
}

/// <summary>
/// Base validator for paginated requests
/// </summary>
public sealed class BasePagedRequestValidator : AbstractValidator<GetConversationsRequestDto>
{
    public BasePagedRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("PageNumber must be greater than 0")
            .When(x => x.PageNumber.HasValue);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100")
            .When(x => x.PageSize.HasValue);
    }
}