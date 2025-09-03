using Axon.Modules.Identity.Domain.ValueObjects;
using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetMyWalletOwnership;

/// <summary>
/// Validator for GetMyWalletOwnership query ensuring valid inputs.
/// </summary>
public sealed class GetMyWalletOwnershipQueryValidator : AbstractValidator<GetMyWalletOwnershipQuery>
{
    public GetMyWalletOwnershipQueryValidator()
    {
        // Must provide either wallet ID or both chain ID and address
        RuleFor(x => x)
            .Must(x => x.IsValid)
            .WithErrorCode("IDENTITY.WALLET.OWNERSHIP.INVALID_INPUT")
            .WithMessage("Either WalletId or both ChainId and RawAddress must be provided.");

        // Validate wallet ID format when provided
        RuleFor(x => x.WalletId)
            .Must(BeValidGuid)
            .When(x => !string.IsNullOrEmpty(x.WalletId))
            .WithErrorCode("IDENTITY.WALLET.ID.INVALID")
            .WithMessage("Wallet ID must be a valid GUID.");

        // Validate chain ID when provided
        RuleFor(x => x.ChainId)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.RawAddress))
            .WithErrorCode("IDENTITY.WALLET.CHAIN.REQUIRED")
            .WithMessage("Chain ID is required when address is provided.")
            .Must(TryValidateChainId)
            .When(x => !string.IsNullOrEmpty(x.ChainId))
            .WithErrorCode("IDENTITY.WALLET.CHAIN.INVALID")
            .WithMessage("Chain ID is invalid.");

        // Validate address format when provided
        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.ChainId))
            .WithErrorCode("IDENTITY.WALLET.ADDRESS.REQUIRED")
            .WithMessage("Address is required when chain ID is provided.")
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.RawAddress))
            .WithErrorCode("IDENTITY.WALLET.ADDRESS.TOO_LONG")
            .WithMessage("Address cannot exceed 100 characters.");
    }

    private static bool BeValidGuid(string? walletId)
    {
        return !string.IsNullOrEmpty(walletId) && Guid.TryParse(walletId, out _);
    }

    private static bool TryValidateChainId(string? chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId)) return false;
        try
        {
            var _ = ChainId.From(chainId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}