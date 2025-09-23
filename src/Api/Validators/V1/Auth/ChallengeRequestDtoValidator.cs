using Axon.Api.Contracts.V1.Auth;
using FluentValidation;

namespace Axon.Api.Validators.V1.Auth;

/// <summary>
/// Validator for challenge request
/// </summary>
public sealed class ChallengeRequestDtoValidator : AbstractValidator<ChallengeRequestDto>
{
    public ChallengeRequestDtoValidator()
    {
        RuleFor(x => x.ChainId)
            .NotEmpty()
            .WithMessage("Chain ID is required")
            .MaximumLength(50)
            .WithMessage("Chain ID must be 50 characters or less");

        RuleFor(x => x.WalletAddress)
            .NotEmpty()
            .WithMessage("Wallet address is required")
            .MaximumLength(100)
            .WithMessage("Wallet address must be 100 characters or less");

        RuleFor(x => x.Audience)
            .MaximumLength(100)
            .WithMessage("Audience must be 100 characters or less")
            .When(x => !string.IsNullOrEmpty(x.Audience));
    }
}