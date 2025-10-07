using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Read-only repository for Wallet query operations.
/// Supports specification-based queries for flexible data retrieval
/// and includes optimized compiled queries for hot paths.
/// </summary>
public interface IWalletReadRepository : ISpecificationReadRepository<Wallet>
{
    /// <summary>
    /// Optimized method for checking wallet existence by chain and address.
    /// Efficient existence check without loading the full aggregate.
    /// </summary>
    Task<bool> ExistsByChainAndAddressAsync(
        ChainId chainId, 
        Address address, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets for a specific chain, with optional filtering and pagination.
    /// Optimized with compiled queries for performance.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetByChainOptimizedAsync(
        ChainId chainId,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Gets wallets that were last seen within a specific time window.
    /// Useful for finding recently active wallets.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetRecentlyActiveAsync(
        TimeSpan withinTimespan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets that have not been seen since a specific time.
    /// Useful for finding inactive/stale wallets.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetInactiveAsync(
        TimeSpan olderThan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of wallets, optionally filtered by chain and deletion status.
    /// Optimized count operation.
    /// </summary>
    Task<int> GetCountOptimizedAsync(
        ChainId? chainId = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches wallets by partial address match.
    /// Useful for address lookup/autocomplete scenarios.
    /// </summary>
    Task<IReadOnlyList<Wallet>> SearchByAddressAsync(
        string partialAddress,
        ChainId? chainId = null,
        bool includeDeleted = false,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about wallet distribution across chains.
    /// Optimized aggregation query.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetChainDistributionAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets by multiple IDs in a single query to avoid N+1 problems.
    /// Optimized batch fetch for GetCurrentUser use case.
    /// Excludes soft-deleted wallets by default.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a wallet using the dual-key lookup (chain ID, address).
    /// This is the primary lookup method for deterministic principal resolution.
    /// ChainId must be in compound format (e.g., "solana:mainnet") containing all network information.
    /// Uses the ux_wallet_chain_addr index for optimal performance.
    /// </summary>
    Task<Wallet?> FindWalletAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default);
}