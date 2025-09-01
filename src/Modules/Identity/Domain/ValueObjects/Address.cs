using System.Text;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Canonical normalized wallet address.
/// Provides chain-specific validation and normalization.
/// Stored in canonical form for identity and uniqueness.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct Address
{
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Address cannot be empty.");

        var trimmed = input.Trim();
        
        if (trimmed.Length < 10)
            return Validation.Invalid("Address too short.");

        if (trimmed.Length > 200)
            return Validation.Invalid("Address cannot exceed 200 characters.");

        // Basic format validation - alphanumeric only
        if (!IsValidAddressFormat(trimmed))
            return Validation.Invalid("Address contains invalid characters. Only alphanumeric characters allowed.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input) => input.Trim();

    private static bool IsValidAddressFormat(string address)
    {
        return address.All(c => char.IsLetterOrDigit(c));
    }

    /// <summary>
    /// Creates and validates an address for a specific chain.
    /// Applies chain-specific normalization and validation rules.
    /// </summary>
    public static Result<Address, Error> CreateForChain(ChainId chainId, string? value)
    {
        if (value is null)
            return Result.Failure<Address, Error>(
                Error.Validation("Address is required.", "WALLET.ADDRESS.REQUIRED"));

        try
        {
            var normalized = NormalizeForChain(chainId, value);
            var validationResult = ValidateForChain(chainId, normalized);
            
            if (validationResult.IsFailure)
                return Result.Failure<Address, Error>(validationResult.Error);

            var address = From(normalized);
            return Result.Success<Address, Error>(address);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<Address, Error>(
                Error.Validation(ex.Message, "WALLET.ADDRESS.INVALID"));
        }
    }

    private static string NormalizeForChain(ChainId chainId, string value)
    {
        var trimmed = value.Trim();

        return chainId.Value.ToLowerInvariant() switch
        {
            "solana" => NormalizeSolanaAddress(trimmed),
            "ethereum" => NormalizeEthereumAddress(trimmed),
            "polygon" => NormalizeEthereumAddress(trimmed), // Polygon uses same format as Ethereum
            _ => trimmed
        };
    }

    private static string NormalizeSolanaAddress(string address)
    {
        // Solana addresses are base58 encoded 32-byte public keys
        // They should remain as-is after validation
        return address;
    }

    private static string NormalizeEthereumAddress(string address)
    {
        // Ethereum addresses start with 0x and are case-insensitive
        if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return "0x" + address.Substring(2).ToLowerInvariant();
        
        return "0x" + address.ToLowerInvariant();
    }

    private static Result<Unit, Error> ValidateForChain(ChainId chainId, string normalizedAddress)
    {
        return chainId.Value.ToLowerInvariant() switch
        {
            "solana" => ValidateSolanaAddress(normalizedAddress),
            "ethereum" => ValidateEthereumAddress(normalizedAddress),
            "polygon" => ValidateEthereumAddress(normalizedAddress),
            _ => Result.Failure<Unit, Error>(Error.Validation($"Validation not implemented for chain '{chainId.Value}'", "WALLET.ADDRESS.CHAIN_NOT_SUPPORTED"))
        };
    }

    private static Result<Unit, Error> ValidateSolanaAddress(string address)
    {
        // Solana addresses are base58 encoded 32-byte public keys (44 characters)
        if (address.Length != 44)
            return Result.Failure<Unit, Error>(
                Error.Validation("Solana address must be exactly 44 characters.", "WALLET.ADDRESS.SOLANA_INVALID_LENGTH"));

        // Check for valid base58 characters
        const string base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        if (!address.All(c => base58Chars.Contains(c, StringComparison.Ordinal)))
            return Result.Failure<Unit, Error>(
                Error.Validation("Solana address contains invalid base58 characters.", "WALLET.ADDRESS.SOLANA_INVALID_FORMAT"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static Result<Unit, Error> ValidateEthereumAddress(string address)
    {
        // Ethereum addresses are 20 bytes = 40 hex chars + 0x prefix = 42 total
        if (!address.StartsWith("0x"))
            return Result.Failure<Unit, Error>(
                Error.Validation("Ethereum address must start with '0x'.", "WALLET.ADDRESS.ETHEREUM_MISSING_PREFIX"));

        if (address.Length != 42)
            return Result.Failure<Unit, Error>(
                Error.Validation("Ethereum address must be exactly 42 characters (including 0x prefix).", "WALLET.ADDRESS.ETHEREUM_INVALID_LENGTH"));

        var hexPart = address.Substring(2);
        if (!IsValidHexString(hexPart))
            return Result.Failure<Unit, Error>(
                Error.Validation("Ethereum address contains invalid hexadecimal characters.", "WALLET.ADDRESS.ETHEREUM_INVALID_FORMAT"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static bool IsValidHexString(string hex)
    {
        return hex.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }
}