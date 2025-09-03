using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Validator for UpdateProfile command.
/// Performs basic shape validation - domain value objects handle specific value validation.
/// </summary>
public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required");

        // At least one field must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.PreferredLanguage) || !string.IsNullOrEmpty(x.RiskTier))
            .WithMessage("Either PreferredLanguage or RiskTier must be provided");

        // Basic format validation - let domain VOs handle specific allowed values
        RuleFor(x => x.PreferredLanguage)
            .NotEmpty()
            .MaximumLength(10)
            .When(x => !string.IsNullOrEmpty(x.PreferredLanguage))
            .WithMessage("PreferredLanguage must be a non-empty string with max 10 characters");

        RuleFor(x => x.RiskTier)
            .NotEmpty()
            .MaximumLength(20)
            .When(x => !string.IsNullOrEmpty(x.RiskTier))
            .WithMessage("RiskTier must be a non-empty string with max 20 characters");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.CorrelationId))
            .WithMessage("CorrelationId must not exceed 255 characters");
    }
}