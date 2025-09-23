namespace BuildingBlocks.Core.Utilities;

/// <summary>
/// Utility for converting between simple and compound chain ID formats.
/// Handles the conversion from external formats (e.g., Dynamic.xyz) to internal compound format.
/// </summary>
public static class ChainIdConverter
{
    /// <summary>
    /// Converts simple chain IDs to compound format with network suffix.
    /// Defaults to mainnet for production safety.
    /// </summary>
    /// <param name="simpleChainId">Simple chain ID (e.g., "solana")</param>
    /// <returns>Compound chain ID (e.g., "solana-mainnet")</returns>
    public static string ConvertToCompoundChainId(string simpleChainId)
    {
        if (string.IsNullOrWhiteSpace(simpleChainId))
            return simpleChainId;

        // If already compound format, return as-is
        if (simpleChainId.Contains('-', StringComparison.Ordinal))
            return simpleChainId;

        // Convert simple chain IDs to compound format with mainnet suffix
        return simpleChainId.ToLowerInvariant() switch
        {
            "solana" => "solana-mainnet",
            "ethereum" => "ethereum-mainnet",
            "polygon" => "polygon-mainnet",
            "arbitrum" => "arbitrum-one", // Arbitrum mainnet is called "arbitrum-one"
            "optimism" => "optimism-mainnet",
            "base" => "base-mainnet",
            "avalanche" => "avalanche-mainnet",
            "binance" => "binance-mainnet",
            _ => $"{simpleChainId}-mainnet" // Default pattern for unknown chains
        };
    }

    /// <summary>
    /// Extracts the network environment from a compound chain ID.
    /// </summary>
    /// <param name="compoundChainId">Compound chain ID (e.g., "solana-mainnet")</param>
    /// <returns>Network environment part (e.g., "mainnet") or null if not compound format</returns>
    public static string? ExtractNetworkEnvironment(string compoundChainId)
    {
        if (string.IsNullOrWhiteSpace(compoundChainId))
            return null;

        var parts = compoundChainId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : null; // Return last part
    }

    /// <summary>
    /// Extracts the base chain from a compound chain ID.
    /// </summary>
    /// <param name="compoundChainId">Compound chain ID (e.g., "solana-mainnet")</param>
    /// <returns>Base chain part (e.g., "solana") or the original if not compound format</returns>
    public static string ExtractBaseChain(string compoundChainId)
    {
        if (string.IsNullOrWhiteSpace(compoundChainId))
            return compoundChainId;

        var parts = compoundChainId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[0] : compoundChainId;
    }
}