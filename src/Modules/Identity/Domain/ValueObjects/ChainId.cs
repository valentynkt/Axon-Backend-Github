using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Represents a blockchain network chain identifier.
/// </summary>
public sealed record ChainId
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
        "binance"
    };

    public string Value { get; }

    private ChainId(string value)
    {
        Value = value;
    }

    public static ChainId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ChainId cannot be null or empty.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        
        if (!IsSupported(normalized))
            throw new ArgumentException($"Unsupported chain: {value}", nameof(value));

        return new ChainId(normalized);
    }

    public static Result<ChainId, Error> TryFrom(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<ChainId, Error>(
                Error.Validation("ChainId cannot be null or empty.", "CHAIN.ID.EMPTY"));

        var normalized = value.Trim().ToLowerInvariant();
        
        if (!IsSupported(normalized))
            return Result.Failure<ChainId, Error>(
                Error.Validation($"Unsupported chain: {value}", "CHAIN.ID.UNSUPPORTED"));

        return Result.Success<ChainId, Error>(new ChainId(normalized));
    }

    public static bool IsSupported(string chainId)
    {
        return !string.IsNullOrWhiteSpace(chainId) && 
               SupportedChains.Contains(chainId.Trim().ToLowerInvariant());
    }

    public static IEnumerable<string> GetSupportedChains() => SupportedChains;

    public static implicit operator string(ChainId chainId) => chainId.Value;
    public static implicit operator ChainId(string value) => From(value);

    public override string ToString() => Value;
}