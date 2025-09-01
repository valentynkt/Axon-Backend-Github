using System.Text.Json;
using System.Text.Json.Nodes;

namespace Axon.Modules.Identity.Domain.Abstractions;

/// <summary>
/// Repository contract for interacting with the Wallet bounded context.
/// Provides read-only access to global wallet catalog for Identity operations.
/// </summary>
public interface IWalletCatalogRepository
{
    /// <summary>
    /// Retrieves a wallet by its chain and address combination.
    /// </summary>
    /// <param name="chain">The blockchain identifier (e.g., "solana", "ethereum")</param>
    /// <param name="address">The wallet address on the specified chain</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The wallet if found, null otherwise</returns>
    Task<Wallet?> GetByChainAddressAsync(
        string chain,
        string address,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts a wallet in the global catalog.
    /// Creates a new wallet if it doesn't exist, updates metadata if it does.
    /// </summary>
    /// <param name="chain">The blockchain identifier</param>
    /// <param name="address">The wallet address</param>
    /// <param name="metadata">Additional metadata about the wallet</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created or updated wallet</returns>
    Task<Wallet> UpsertAsync(
        string chain,
        string address,
        JsonObject metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves multiple wallets by their IDs.
    /// Used for batch operations and validation.
    /// </summary>
    /// <param name="walletIds">The wallet IDs to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to wallet objects</returns>
    Task<Dictionary<WalletId, Wallet>> GetByIdsAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a wallet exists by ID.
    /// </summary>
    /// <param name="walletId">The wallet ID to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the wallet exists</returns>
    Task<bool> ExistsAsync(
        WalletId walletId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the last seen timestamp for a wallet.
    /// Called when wallet activity is detected.
    /// </summary>
    /// <param name="walletId">The wallet ID</param>
    /// <param name="lastSeenAt">The timestamp when the wallet was last seen</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the update was successful</returns>
    Task<bool> UpdateLastSeenAsync(
        WalletId walletId,
        DateTimeOffset lastSeenAt,
        CancellationToken ct = default);
}

/// <summary>
/// Wallet entity from the Wallet bounded context.
/// This is a read-only representation for Identity domain operations.
/// </summary>
public sealed record Wallet(
    WalletId WalletId,
    string Chain,
    string Address,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    JsonObject Metadata,
    string[]? Tags = null,
    DateTimeOffset? DeletedAt = null)
{
    public bool IsDeleted => DeletedAt.HasValue;
    public string ChainAddress => $"{Chain}:{Address}";
}