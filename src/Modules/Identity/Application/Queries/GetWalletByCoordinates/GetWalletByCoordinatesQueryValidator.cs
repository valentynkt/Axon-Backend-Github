using Axon.Modules.Identity.Domain.ValueObjects;
using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetWalletByCoordinates;

/// <summary>
/// Validator for GetWalletByCoordinates query ensuring chain and address are valid.
/// </summary>
public sealed class GetWalletByCoordinatesQueryValidator : AbstractValidator<GetWalletByCoordinatesQuery>
{
    public GetWalletByCoordinatesQueryValidator()
    {
        RuleFor(x => x.ChainId)
            .NotEmpty()
            .WithErrorCode("IDENTITY.WALLET.CHAIN.REQUIRED")
            .WithMessage("Chain ID is required.")
            .Must(TryValidateChainId)
            .WithErrorCode("IDENTITY.WALLET.CHAIN.INVALID")
            .WithMessage("Chain ID is invalid.");

        RuleFor(x => x.RawAddress)
            .NotEmpty()
            .WithErrorCode("IDENTITY.WALLET.ADDRESS.REQUIRED")
            .WithMessage("Address is required.")
            .MaximumLength(100)
            .WithErrorCode("IDENTITY.WALLET.ADDRESS.TOO_LONG")
            .WithMessage("Address cannot exceed 100 characters.");
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