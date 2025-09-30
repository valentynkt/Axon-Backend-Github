using FluentValidation;
using System.Text.Json;

namespace Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;

/// <summary>
/// Validator for VerifyWalletSignatureCommand that ensures all required fields
/// are present and properly formatted before attempting signature verification.
/// Validates CAIP-2 chain ID, wallet address, JSON message format, and cryptographic parameters.
/// </summary>
public sealed class VerifyWalletSignatureCommandValidator : AbstractValidator<VerifyWalletSignatureCommand>
{
    private static readonly string[] SupportedBlockchains = { "eip155", "solana", "cosmos", "polkadot" };

    public VerifyWalletSignatureCommandValidator()
    {
        RuleFor(x => x.ChainId)
            .NotEmpty()
            .WithMessage("Chain ID is required")
            .Must(BeValidChainIdFormat)
            .WithMessage("Chain ID must be in format '{blockchain}:{network}' (e.g., 'eip155:1', 'solana:mainnet')");

        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Wallet address is required")
            .MinimumLength(26)
            .WithMessage("Wallet address must be at least 26 characters")
            .MaximumLength(256)
            .WithMessage("Wallet address cannot exceed 256 characters");

        RuleFor(x => x.SignedMessage)
            .NotEmpty()
            .WithMessage("Signed message is required")
            .Must(BeValidJson)
            .WithMessage("Signed message must be valid JSON");

        RuleFor(x => x.Signature)
            .NotEmpty()
            .WithMessage("Signature is required")
            .MinimumLength(20)
            .WithMessage("Signature must be at least 20 characters");

        RuleFor(x => x.Mac)
            .NotEmpty()
            .WithMessage("MAC (Message Authentication Code) is required")
            .MinimumLength(10)
            .WithMessage("MAC must be at least 10 characters");

        RuleFor(x => x.Mkv)
            .NotEmpty()
            .WithMessage("MKV (MAC Key Version) is required")
            .MinimumLength(10)
            .WithMessage("MKV must be at least 10 characters");
    }

    private static bool BeValidChainIdFormat(string chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId))
            return false;

        // CAIP-2 format: {blockchain}:{network}
        var parts = chainId.Split(':');
        if (parts.Length != 2)
            return false;

        var blockchain = parts[0];
        var network = parts[1];

        return !string.IsNullOrWhiteSpace(blockchain)
               && !string.IsNullOrWhiteSpace(network)
               && SupportedBlockchains.Contains(blockchain.ToLowerInvariant());
    }

    private static bool BeValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}