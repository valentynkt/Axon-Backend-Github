using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Repository contract for AxonPrincipal aggregate persistence and retrieval.
/// </summary>
public interface IAxonPrincipalRepository : IWriteRepository<AxonPrincipal, AxonId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management.
    /// </summary>
    IWriteUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Finds a principal by their identity credential.
    /// Used during authentication flows to resolve existing principals.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The principal if found, null otherwise</returns>
    Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default);

    /// <summary>
    /// Finds a principal that owns the specified wallet.
    /// Used for wallet-first resolution during authentication.
    /// </summary>
    /// <param name="walletId">The wallet ID to search for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The principal that owns the wallet, null if not found</returns>
    Task<AxonPrincipal?> FindByWalletIdAsync(
        WalletId walletId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a wallet is already owned by any principal with verified signing ownership.
    /// Used to enforce the global single verified owner rule.
    /// Only returns true for verified signing ownership, not watch-only.
    /// </summary>
    /// <param name="walletId">The wallet ID to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the wallet is owned by any principal with verified signing</returns>
    Task<bool> IsWalletOwnedByVerifiedSigningAsync(
        WalletId walletId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a credential already exists for any principal.
    /// Used to enforce credential uniqueness across all principals.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the credential exists for any principal</returns>
    Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default);

    /// <summary>
    /// Finds all principals that have verified signing ownership of the specified wallets.
    /// Used for Dynamic bundle processing to check existing ownership.
    /// Only returns principals with verified signing ownership, not watch-only.
    /// </summary>
    /// <param name="walletIds">The wallet IDs to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to their owning principals (verified signing only)</returns>
    Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default);

    /// <summary>
    /// Gets principals by their email hash.
    /// Used for email-based lookups when available.
    /// </summary>
    /// <param name="emailHash">The SHA256 hash of the email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Principals with the specified email hash</returns>
    Task<IReadOnlyCollection<AxonPrincipal>> FindByEmailHashAsync(
        EmailHash emailHash,
        CancellationToken ct = default);
}