using Axon.Modules.Identity.Domain.ValueObjects;
using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.SearchWalletsByAddress;

/// <summary>
/// Validator for SearchWalletsByAddress query ensuring search parameters are valid.
/// </summary>
public sealed class SearchWalletsByAddressQueryValidator : AbstractValidator<SearchWalletsByAddressQuery>
{
    public SearchWalletsByAddressQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithErrorCode("IDENTITY.WALLET.SEARCH.QUERY_REQUIRED")
            .WithMessage("Search query is required.")
            .Length(3, 100)
            .WithErrorCode("IDENTITY.WALLET.SEARCH.INVALID_INPUT")
            .WithMessage("Search query must be between 3 and 100 characters.");

        RuleFor(x => x.Take)
            .GreaterThan(0)
            .WithErrorCode("IDENTITY.WALLET.SEARCH.INVALID_INPUT")
            .WithMessage("Take must be greater than 0.")
            .LessThanOrEqualTo(50)
            .WithErrorCode("IDENTITY.WALLET.SEARCH.INVALID_INPUT")
            .WithMessage("Take cannot exceed 50.");

        RuleFor(x => x.ChainId)
            .NotEmpty()
            .When(x => x.ChainId != null)
            .WithErrorCode("IDENTITY.WALLET.SEARCH.INVALID_INPUT")
            .WithMessage("Chain ID cannot be empty when provided.")
            .Must(TryValidateChainId)
            .When(x => !string.IsNullOrEmpty(x.ChainId))
            .WithErrorCode("IDENTITY.WALLET.CHAIN.INVALID")
            .WithMessage("Chain ID is invalid.");
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