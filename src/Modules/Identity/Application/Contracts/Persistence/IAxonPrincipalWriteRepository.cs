using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Write repository contract for AxonPrincipal aggregate persistence operations.
/// </summary>
public interface IAxonPrincipalWriteRepository : IWriteRepository<AxonPrincipal, AxonUserId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management.
    /// </summary>
    IWriteUnitOfWork<IdentityModule> UnitOfWork { get; }

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
    /// Finds all principals that have verified signing ownership of the specified wallets.
    /// Returns tracked entities for modification during Dynamic bundle processing.
    /// Only returns principals with verified signing ownership, not watch-only.
    /// Note: For read-only validation, use IAxonPrincipalReadRepository.FindVerifiedSigningOwnersAsync instead.
    /// </summary>
    /// <param name="walletIds">The wallet IDs to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to their owning principals (verified signing only)</returns>
    Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a credential is already taken by another principal.
    /// Used during write operations to validate credential uniqueness before persistence.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="excludePrincipalId">Principal to exclude from check (when updating existing principal)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the credential is taken by another principal, false otherwise</returns>
    Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        AxonUserId? excludePrincipalId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes all pending ownerships for the specified wallet across all principals.
    /// Used to maintain exclusivity constraint when a wallet ownership is verified.
    /// </summary>
    /// <param name="walletId">The wallet ID for which to revoke pending ownerships</param>
    /// <param name="excludePrincipalId">Principal to exclude from revocation (the one getting verified)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of ownerships revoked</returns>
    Task<int> RevokePendingOwnershipsForWalletAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all principals with pending ownership of the specified wallet.
    /// </summary>
    /// <param name="walletId">The wallet to check</param>
    /// <param name="excludePrincipalId">Principal to exclude from results</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of principals with pending ownership</returns>
    Task<List<AxonPrincipal>> GetPrincipalsWithPendingOwnershipAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if another principal has verified signing ownership of the wallet.
    /// </summary>
    /// <param name="walletId">The wallet to check</param>
    /// <param name="excludePrincipalId">Principal to exclude from check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if another principal has verified signing ownership</returns>
    Task<bool> HasVerifiedSigningOwnershipAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a tracked principal by ID if it's already in the ChangeTracker, otherwise returns null.
    /// Used to prevent duplicate entity tracking when the same principal is loaded multiple times in a request.
    /// </summary>
    /// <param name="principalId">The principal ID to check</param>
    /// <returns>The tracked principal if found in ChangeTracker, null otherwise</returns>
    AxonPrincipal? GetTrackedPrincipal(AxonUserId principalId);
}