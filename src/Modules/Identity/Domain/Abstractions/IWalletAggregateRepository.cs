using Axon.Modules.Identity.Domain.ValueObjects;
using WalletAggregate = Axon.Modules.Identity.Domain.Aggregates.Wallet.Wallet;

namespace Axon.Modules.Identity.Domain.Abstractions;

/// <summary>
/// Repository interface for Wallet aggregate persistence.
/// Defines contract for wallet storage operations and queries.
/// </summary>
public interface IWalletAggregateRepository
{
    /// <summary>
    /// Adds a new wallet to the repository.
    /// </summary>
    Task AddAsync(WalletAggregate wallet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing wallet in the repository.
    /// </summary>
    Task UpdateAsync(WalletAggregate wallet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a wallet by its unique identifier.
    /// </summary>
    Task<WalletAggregate?> GetByIdAsync(WalletId walletId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a wallet by chain and address combination.
    /// Used to enforce global uniqueness constraint (W1).
    /// </summary>
    Task<WalletAggregate?> GetByChainAndAddressAsync(
        ChainId chainId, 
        Address address, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a wallet exists for the given chain and address.
    /// Efficient existence check without loading the full aggregate.
    /// </summary>
    Task<bool> ExistsAsync(
        ChainId chainId, 
        Address address, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple wallets by their identifiers.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets for a specific chain, with optional filtering and pagination.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> GetByChainAsync(
        ChainId chainId,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets that have a specific tag.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> GetByTagAsync(
        Tag tag,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets that were last seen within a specific time window.
    /// Useful for finding recently active wallets.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> GetRecentlyActiveAsync(
        TimeSpan withinTimespan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets wallets that have not been seen since a specific time.
    /// Useful for finding inactive/stale wallets.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> GetInactiveAsync(
        TimeSpan olderThan,
        bool includeDeleted = false,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of wallets, optionally filtered by chain and deletion status.
    /// </summary>
    Task<int> GetCountAsync(
        ChainId? chainId = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches wallets by partial address match.
    /// Useful for address lookup/autocomplete scenarios.
    /// </summary>
    Task<IReadOnlyList<WalletAggregate>> SearchByAddressAsync(
        string partialAddress,
        ChainId? chainId = null,
        bool includeDeleted = false,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about wallet distribution across chains.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetChainDistributionAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a wallet from the repository (hard delete).
    /// Should only be used for data cleanup, not normal business operations.
    /// </summary>
    Task RemoveAsync(WalletAggregate wallet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the repository.
    /// Used for unit of work pattern implementation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}