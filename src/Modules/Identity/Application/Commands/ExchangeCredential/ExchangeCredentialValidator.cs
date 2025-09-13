using Axon.Modules.Identity.Application.DTOs.Exchange;
using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Validator for ExchangeCredentialCommand that ensures the normalized user data 
/// meets all requirements for safe exchange processing.
/// </summary>
public sealed class ExchangeCredentialCommandValidator : AbstractValidator<ExchangeCredentialCommand>
{
    public ExchangeCredentialCommandValidator()
    {
        RuleFor(x => x.UserData)
            .NotNull()
            .WithMessage("User data is required");

        When(x => x.UserData != null, () =>
        {
            RuleFor(x => x.UserData.UserId)
                .NotEmpty()
                .WithMessage("User ID is required")
                .MaximumLength(256)
                .WithMessage("User ID must not exceed 256 characters");

            RuleFor(x => x.UserData.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Email must be a valid email address")
                .MaximumLength(320)
                .WithMessage("Email must not exceed 320 characters");

            RuleFor(x => x.UserData.EnvironmentId)
                .NotEmpty()
                .WithMessage("Environment ID is required")
                .MaximumLength(256)
                .WithMessage("Environment ID must not exceed 256 characters");

            RuleFor(x => x.UserData.Wallets)
                .NotNull()
                .WithMessage("Wallets list is required");

            When(x => x.UserData.Wallets != null, () =>
            {
                RuleForEach(x => x.UserData.Wallets)
                    .SetValidator(new ExchangeWalletDataValidator());
            });
        });
    }
}

/// <summary>
/// Validator for individual wallet data within the exchange request.
/// Ensures each wallet has valid address and chain information.
/// </summary>
public sealed class ExchangeWalletDataValidator : AbstractValidator<ExchangeWalletData>
{
    public ExchangeWalletDataValidator()
    {
        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Wallet address is required")
            .MaximumLength(256)
            .WithMessage("Wallet address must not exceed 256 characters")
            .Must(BeValidWalletAddress)
            .WithMessage("Wallet address must contain only valid characters");

        RuleFor(x => x.Chain)
            .NotEmpty()
            .WithMessage("Chain identifier is required")
            .MaximumLength(50)
            .WithMessage("Chain identifier must not exceed 50 characters")
            .Must(BeValidChainIdentifier)
            .WithMessage("Chain identifier must be valid (letters, numbers, hyphens, underscores only)");

        When(x => !string.IsNullOrWhiteSpace(x.WalletName), () =>
        {
            RuleFor(x => x.WalletName)
                .MaximumLength(100)
                .WithMessage("Wallet name must not exceed 100 characters");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Provider), () =>
        {
            RuleFor(x => x.Provider)
                .MaximumLength(50)
                .WithMessage("Provider name must not exceed 50 characters");
        });
    }

    private static bool BeValidWalletAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // Basic validation: allow alphanumeric and common blockchain address characters
        // Supports hex addresses (0x prefix) and base58 addresses
        return address.All(c => char.IsLetterOrDigit(c) || c == 'x' || c == 'X')
               && address.Length >= 10 && address.Length <= 200;
    }

    private static bool BeValidChainIdentifier(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return false;

        // Chain identifiers should be alphanumeric with hyphens and underscores
        return chain.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
}

