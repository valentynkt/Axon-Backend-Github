using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Validator for the refactored GetMyPrincipalQuery that uses AxonPrincipalId directly.
/// Simplified validation for the SOLID-compliant architecture.
/// </summary>
public sealed class GetMyPrincipalValidator : AbstractValidator<GetMyPrincipalQuery>
{
    public GetMyPrincipalValidator()
    {
        RuleFor(x => x.PrincipalId)
            .Must(id => id.Value != Guid.Empty)
            .WithMessage("Principal ID cannot be empty");
    }
}