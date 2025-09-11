using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Validator for GetMyPrincipalQuery to ensure credential parameters are valid.
/// </summary>
public sealed class GetMyPrincipalValidator : AbstractValidator<GetMyPrincipalQuery>
{
    public GetMyPrincipalValidator()
    {
        RuleFor(x => x.ProviderType)
            .NotNull()
            .WithMessage("Provider type is required");

        RuleFor(x => x.Issuer)
            .NotEmpty()
            .WithMessage("Issuer is required")
            .MaximumLength(255)
            .WithMessage("Issuer cannot exceed 255 characters");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .MaximumLength(255)
            .WithMessage("Subject cannot exceed 255 characters");

        RuleFor(x => x.IfNoneMatch)
            .MaximumLength(64)
            .WithMessage("If-None-Match header cannot exceed 64 characters")
            .When(x => !string.IsNullOrEmpty(x.IfNoneMatch));
    }
}