using System.Text.RegularExpressions;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Simple normalized wallet address value object with enhanced validation.
/// Creation: <c>Address.From("...")</c> (throws on invalid)
/// Non-throwing: <c>Address.Create("...")</c> (returns Result)
/// JSON: STJ converter generated
/// EF Core: value converter generated  
/// TypeConverter: generated (useful for binding, config, etc.)
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

        // Validate format for known address types
        if (IsEthereumFormat(trimmed))
        {
            if (!EvmAddressPattern().IsMatch(trimmed))
                return Validation.Invalid("Invalid Ethereum address format.");
        }
        else if (IsSolanaFormat(trimmed))
        {
            if (!SolanaAddressPattern().IsMatch(trimmed))
                return Validation.Invalid("Invalid Solana address format.");
        }
        else
        {
            // For unknown formats, apply basic security validation
            if (ContainsMaliciousCharacters(trimmed))
                return Validation.Invalid("Address contains invalid characters.");
        }

        return Validation.Ok;
    }

    private static string NormalizeInput(string input)
    {
        var trimmed = input.Trim();
        
        // Don't normalize case - preserve original case for display/storage
        // Validation will handle format checking
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
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<Address, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Address, Error>(
                Error.Validation("Address cannot be empty.", "ADDRESS.EMPTY"));
        }

        var trimmed = value.Trim();
        
        if (trimmed.Length < 10)
        {
            return Result.Failure<Address, Error>(
                Error.Validation("Address too short.", "ADDRESS.TOO_SHORT"));
        }

        if (trimmed.Length > 200)
        {
            return Result.Failure<Address, Error>(
                Error.Validation("Address too long.", "ADDRESS.TOO_LONG"));
        }

        // Use generated TryParse; provider null is fine
        return TryParse(value, provider: null, out var vo)
            ? Result.Success<Address, Error>(vo)
            : Result.Failure<Address, Error>(
                Error.Validation($"Invalid address format: {value}", "ADDRESS.INVALID"));
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

    private static bool IsEthereumFormat(string address)
    {
        // If it starts with 0x, treat it as Ethereum regardless of length
        return address.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSolanaFormat(string address)
    {
        // If it doesn't start with 0x and is in the valid length range, treat as Solana
        return !address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && 
               address.Length >= 32 && address.Length <= 44;
    }

    private static bool ContainsMaliciousCharacters(string input)
    {
        // Check for common malicious patterns
        return input.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("../", StringComparison.Ordinal) ||
               input.Any(c => char.IsControl(c) && c != '\t' && c != '\n' && c != '\r') ||
               input.Any(c => c > 127); // Non-ASCII characters
    }
}