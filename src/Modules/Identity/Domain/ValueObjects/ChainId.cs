using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a blockchain network chain identifier with validation.
/// Creation: <c>ChainId.From("...")</c> (throws on invalid)
/// Non-throwing: <c>ChainId.Create("...")</c> (returns Result)
/// JSON: STJ converter generated
/// EF Core: value converter generated  
/// TypeConverter: generated (useful for binding, config, etc.)
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ChainId
{
    // Supported chains (can be extended)
    private static readonly HashSet<string> SupportedChains = new(StringComparer.OrdinalIgnoreCase)
    {
        "solana",
        "ethereum",
        "polygon",
        "arbitrum",
        "optimism",
        "base",
        "avalanche",
        "binance",
        // Compound chain identifiers that tests expect
        "ethereum-mainnet",
        "ethereum-goerli",
        "ethereum-sepolia",
        "solana-mainnet", 
        "solana-devnet",
        "solana-testnet",
        "polygon-mainnet",
        "polygon-mumbai",
        "arbitrum-one",
        "arbitrum-goerli",
        "optimism-mainnet",
        "base-mainnet"
    };

    // Vogen will call this before Validate and before storing the value
    private static string NormalizeInput(string input) => input.Trim().ToLowerInvariant();

    // Vogen passes the normalized input here
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("ChainId cannot be null or empty.");

        if (!SupportedChains.Contains(input))
            return Validation.Invalid($"Unsupported chain: {input}");

        return Validation.Ok;
    }

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<ChainId, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ChainId, Error>(
                Error.Validation("ChainId cannot be null or empty.", "CHAIN.ID.EMPTY"));
        }

        var normalized = NormalizeInput(value);
        
        if (!SupportedChains.Contains(normalized))
        {
            return Result.Failure<ChainId, Error>(
                Error.Validation($"Unsupported chain: {value}", "CHAIN.ID.UNSUPPORTED"));
        }

        // Use generated TryParse; provider null is fine
        return TryParse(value, provider: null, out var vo)
            ? Result.Success<ChainId, Error>(vo)
            : Result.Failure<ChainId, Error>(
                Error.Validation($"Invalid chain: {value}", "CHAIN.ID.INVALID"));
    }

    public static bool IsSupported(string chainId)
    {
        return !string.IsNullOrWhiteSpace(chainId) && 
               SupportedChains.Contains(chainId.Trim().ToLowerInvariant());
    }

    public static IEnumerable<string> GetSupportedChains() => SupportedChains;

    public override string ToString() => Value;
}