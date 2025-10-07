using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.GenerateChallenge;

/// <summary>
/// Validator for GenerateChallengeCommand that ensures the chain ID and wallet address
/// are valid before generating a challenge for signature verification.
/// Validates CAIP-2 chain ID format and basic address structure.
/// </summary>
public sealed class GenerateChallengeCommandValidator : AbstractValidator<GenerateChallengeCommand>
{
    private static readonly string[] SupportedBlockchains = { "ethereum", "solana", "polygon", "arbitrum", "optimism", "base", "avalanche", "binance" };

    public GenerateChallengeCommandValidator()
    {
        RuleFor(x => x.ChainId)
            .NotEmpty()
            .WithMessage("Chain ID is required")
            .Must(BeValidChainIdFormat)
            .WithMessage("Chain ID must be in format '{blockchain}:{network}' (e.g., 'ethereum:mainnet', 'solana:mainnet')");

        RuleFor(x => x.WalletAddress)
            .NotEmpty()
            .WithMessage("Wallet address is required")
            .MinimumLength(26)
            .WithMessage("Wallet address must be at least 26 characters")
            .MaximumLength(256)
            .WithMessage("Wallet address cannot exceed 256 characters");

        RuleFor(x => x.Audience)
            .MaximumLength(256)
            .WithMessage("Audience cannot exceed 256 characters")
            .Must(BeValidUrlFormat)
            .WithMessage("Audience must be a valid URL")
            .When(x => !string.IsNullOrEmpty(x.Audience));
    }

    private static bool BeValidChainIdFormat(string chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId))
            return false;

        // Chain ID format: {blockchain}:{network}
        // Examples: ethereum:mainnet, solana:mainnet, ethereum:1 (mainnet alternative)
        var parts = chainId.Split(':');
        if (parts.Length != 2)
            return false;

        var blockchain = parts[0];
        var network = parts[1];

        // Both parts must be non-empty and blockchain must be supported
        return !string.IsNullOrWhiteSpace(blockchain)
               && !string.IsNullOrWhiteSpace(network)
               && SupportedBlockchains.Contains(blockchain.ToLowerInvariant());
    }

    private static bool BeValidUrlFormat(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true; // Null/empty is valid when field is optional

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}