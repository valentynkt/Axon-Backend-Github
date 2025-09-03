using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetWallet;

/// <summary>
/// Validator for GetWallet query ensuring wallet ID is provided and valid.
/// </summary>
public sealed class GetWalletQueryValidator : AbstractValidator<GetWalletQuery>
{
    public GetWalletQueryValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithErrorCode("IDENTITY.WALLET.ID.REQUIRED")
            .WithMessage("Wallet ID is required.");

        RuleFor(x => x.WalletId)
            .Must(BeValidGuid)
            .When(x => !string.IsNullOrEmpty(x.WalletId))
            .WithErrorCode("IDENTITY.WALLET.ID.INVALID")
            .WithMessage("Wallet ID must be a valid GUID.");
    }

    private static bool BeValidGuid(string? walletId)
    {
        return !string.IsNullOrEmpty(walletId) && Guid.TryParse(walletId, out _);
    }
}