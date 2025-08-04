using FluentValidation;

namespace Axon.Modules.Chat.Application.Queries.GetConversationHistory;

/// <summary>
/// Validator for GetConversationHistoryQuery
/// </summary>
public sealed class GetConversationHistoryValidator : AbstractValidator<GetConversationHistoryQuery>
{
    public GetConversationHistoryValidator()
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
    }
}