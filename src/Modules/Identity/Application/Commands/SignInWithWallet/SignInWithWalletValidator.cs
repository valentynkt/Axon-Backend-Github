using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.SignInWithWallet;

/// <summary>
/// Validator for SignInWithWallet command.
/// </summary>
public sealed class SignInWithWalletValidator : AbstractValidator<SignInWithWalletCommand>
{
    public SignInWithWalletValidator()
    {
        RuleFor(x => x.ChainId)
            .NotNull()
            .WithMessage("Chain ID is required.");

        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .WithMessage("Wallet address is required.")
            .MaximumLength(100)
            .WithMessage("Wallet address cannot exceed 100 characters.");

        RuleFor(x => x.Signature)
            .NotEmpty()
            .WithMessage("Signature is required.")
            .MaximumLength(1000)
            .WithMessage("Signature cannot exceed 1000 characters.");

        RuleFor(x => x.ChallengeId)
            .NotEmpty()
            .WithMessage("Challenge ID is required.")
            .MaximumLength(100)
            .WithMessage("Challenge ID cannot exceed 100 characters.");

        RuleFor(x => x.Label)
            .MaximumLength(100)
            .WithMessage("Label cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Label));

        RuleFor(x => x.AccessMode)
            .Must(mode => string.IsNullOrEmpty(mode) || 
                         mode.Equals("signing", StringComparison.OrdinalIgnoreCase) || 
                         mode.Equals("watch_only", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Access mode must be 'signing' or 'watch_only'.")
            .When(x => !string.IsNullOrEmpty(x.AccessMode));


    }
}