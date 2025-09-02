using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Repository for Wallet aggregate persistence
/// </summary>
public interface IWalletRepository : IWriteRepository<Wallet, WalletId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management
    /// </summary>
    IWriteUnitOfWork UnitOfWork { get; }

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
}