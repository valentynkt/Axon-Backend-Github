namespace BuildingBlocks.Core.Utilities;

/// <summary>
/// Utility for converting between simple and compound chain ID formats.
/// Uses colon separator (e.g., "solana:mainnet") and enforces mainnet-only policy.
/// </summary>
public static class ChainIdConverter
{
    /// <summary>
    /// Converts simple chain IDs to compound format with mainnet network.
    /// Enforces mainnet-only policy - rejects testnet/devnet.
    /// </summary>
    /// <param name="simpleChainId">Simple chain ID (e.g., "solana") or compound (e.g., "solana:mainnet")</param>
    /// <returns>Compound chain ID (e.g., "solana:mainnet")</returns>
    /// <exception cref="ArgumentException">Thrown when non-mainnet network is provided</exception>
    public static string ConvertToCompoundChainId(string simpleChainId)
    {
        if (string.IsNullOrWhiteSpace(simpleChainId))
            return simpleChainId;

        var normalized = simpleChainId.ToLowerInvariant().Trim();

        // If already in compound format with colon
        if (normalized.Contains(':', StringComparison.Ordinal))
        {
            var parts = normalized.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid chain ID format: {simpleChainId}. Expected 'chain' or 'chain:mainnet'");

            var network = parts[1];

            // Enforce mainnet-only policy
            if (network != "mainnet" && network != "1") // Allow ethereum:1 as mainnet
                throw new ArgumentException($"Only mainnet networks are supported. Received: {simpleChainId}");

            return normalized; // Return as-is if valid
        }

        // Convert simple chain IDs to compound format with mainnet
        return normalized switch
        {
            "solana" => "solana:mainnet",
            "ethereum" => "ethereum:mainnet",
            "polygon" => "polygon:mainnet",
            "arbitrum" => "arbitrum:mainnet",
            "optimism" => "optimism:mainnet",
            "base" => "base:mainnet",
            "avalanche" => "avalanche:mainnet",
            "binance" => "binance:mainnet",
            _ => $"{normalized}:mainnet" // Default pattern for unknown chains
        };
    }

    /// <summary>
    /// Extracts the base chain from a compound chain ID.
    /// </summary>
    /// <param name="compoundChainId">Compound chain ID (e.g., "solana:mainnet")</param>
    /// <returns>Base chain part (e.g., "solana") or the original if not compound format</returns>
    public static string ExtractBaseChain(string compoundChainId)
    {
        if (string.IsNullOrWhiteSpace(compoundChainId))
            return compoundChainId;

        var parts = compoundChainId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[0] : compoundChainId;
    }
}