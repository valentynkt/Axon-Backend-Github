using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Validator for GetCurrentUserQuery
/// </summary>
public sealed class GetCurrentUserQueryValidator : AbstractValidator<GetCurrentUserQuery>
{
    public GetCurrentUserQueryValidator()
    {
        RuleFor(x => x.Principal)
            .NotNull()
            .WithMessage("Principal is required");

        RuleFor(x => x.Principal.Identity)
            .NotNull()
            .WithMessage("Principal identity is required")
            .When(x => x.Principal != null);

        RuleFor(x => x.Principal.Identity!.IsAuthenticated)
            .Equal(true)
            .WithMessage("User must be authenticated")
            .When(x => x.Principal?.Identity != null);
    }
}