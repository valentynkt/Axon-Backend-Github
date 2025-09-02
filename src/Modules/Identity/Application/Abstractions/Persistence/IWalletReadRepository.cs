using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Abstractions.Persistence;

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
    /// Gets wallets that have a specific tag.
    /// Optimized with compiled queries for tag-based lookups.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetByTagOptimizedAsync(
        Tag tag,
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
}