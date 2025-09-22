using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Write repository for Wallet aggregate persistence operations
/// </summary>
public interface IWalletWriteRepository : IWriteRepository<Wallet, WalletId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management
    /// </summary>
    IWriteUnitOfWork<IdentityModule> UnitOfWork { get; }

    /// <summary>
    /// Gets a wallet by chain and address combination.
    /// Used to enforce global uniqueness constraint (W1).
    /// </summary>
    Task<Wallet?> GetByChainAndAddressAsync(
        ChainId chainId, 
        Address address, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple wallets by their identifiers.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures multiple wallets exist by their chain and address combinations in batch.
    /// Creates wallets that don't exist, returns all wallet IDs.
    /// Used during exchange operations to prevent N+1 queries.
    /// </summary>
    /// <param name="walletSpecs">Chain and address combinations to ensure</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping chain/address pairs to wallet IDs</returns>
    Task<IReadOnlyDictionary<(string chainId, Address address), WalletId>> EnsureManyByChainAndAddressAsync(
        IEnumerable<(string chainId, Address address)> walletSpecs,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts a wallet with the given network environment, chain ID, and address.
    /// Uses INSERT ... ON CONFLICT DO NOTHING pattern for race condition protection.
    /// Returns the wallet (either newly created or existing).
    /// </summary>
    Task<Wallet> UpsertWalletAsync(
        NetworkEnvironment networkEnvironment,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default);
}