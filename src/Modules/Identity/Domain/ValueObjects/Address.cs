using System.Text.RegularExpressions;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Simple normalized wallet address value object with enhanced validation.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct Address
{
    // Regex patterns for different blockchain address formats
    [GeneratedRegex(@"^[1-9A-HJ-NP-Za-km-z]{32,44}$")]
    private static partial Regex SolanaAddressPattern();
    
    [GeneratedRegex(@"^0x[a-fA-F0-9]{40}$")]
    private static partial Regex EvmAddressPattern();

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Address cannot be empty.");

        var trimmed = input.Trim();
        
        if (trimmed.Length < 10)
            return Validation.Invalid("Address too short.");

        if (trimmed.Length > 200)
            return Validation.Invalid("Address too long.");

        return Validation.Ok;
    }

    private static string NormalizeInput(string input)
    {
        var trimmed = input.Trim();
        
        // Normalize EVM addresses to lowercase
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToLowerInvariant();
        }
        
        // Keep Solana addresses as-is (case-sensitive base58)
        return trimmed;
    }

    /// <summary>
    /// Validates if the address format matches the expected pattern for a given chain.
    /// </summary>
    public bool IsValidForChain(string chainId)
    {
        var chain = chainId?.ToLowerInvariant();
        
        return chain switch
        {
            "solana" => SolanaAddressPattern().IsMatch(Value),
            "ethereum" or "polygon" or "arbitrum" or "optimism" or "base" or "avalanche" or "binance" 
                => EvmAddressPattern().IsMatch(Value),
            _ => true // Allow any format for unknown chains
        };
    }

    /// <summary>
    /// Gets a shortened version of the address for display (e.g., "0x1234...5678").
    /// </summary>
    public string ToShortDisplay(int prefixLength = 6, int suffixLength = 4)
    {
        if (Value.Length <= prefixLength + suffixLength + 3)
            return Value;
        
        return $"{Value[..prefixLength]}...{Value[^suffixLength..]}";
    }
}