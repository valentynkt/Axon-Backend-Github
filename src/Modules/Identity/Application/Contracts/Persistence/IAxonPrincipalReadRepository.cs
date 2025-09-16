using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Read-only repository for AxonPrincipal query operations.
/// Supports specification-based queries for flexible data retrieval
/// and includes optimized compiled queries for hot paths.
/// </summary>
public interface IAxonPrincipalReadRepository : ISpecificationReadRepository<AxonPrincipal>
{
    /// <summary>
    /// Gets principals by provider type for reporting/analytics.
    /// Optimized method with pagination support.
    /// </summary>
    Task<IReadOnlyList<AxonPrincipal>> GetByProviderTypeAsync(
        ProviderType providerType,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts principals by provider type.
    /// </summary>
    Task<int> CountByProviderTypeAsync(
        ProviderType providerType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recently created principals for monitoring/analytics.
    /// </summary>
    Task<IReadOnlyList<AxonPrincipal>> GetRecentlyCreatedAsync(
        TimeSpan within,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets principals that own wallets on specific chains.
    /// Useful for chain-specific analytics and reporting.
    /// </summary>
    Task<IReadOnlyList<AxonPrincipal>> GetWalletOwnersByChainAsync(
        string chainId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a principal by ID with active wallet ownerships included.
    /// Optimized single-query method for GetCurrentUser use case.
    /// Excludes soft-deleted principals and ownerships.
    /// </summary>
    Task<AxonPrincipal?> GetByIdWithActiveOwnershipsAsync(
        AxonId axonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a principal by credential information from JWT claims.
    /// Used to resolve authenticated user's principal for /auth/me queries.
    /// </summary>
    Task<AxonPrincipal?> FindByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the deterministic fingerprint/ETag for a principal.
    /// Includes principal.updated_at + max verified and signing ownership.updated_at + max chain_default.updated_at.
    /// Used for efficient cache validation with If-None-Match header.
    /// </summary>
    Task<string> GetPrincipalFingerprintAsync(
        AxonId principalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a wallet is already owned by any principal with verified signing ownership.
    /// Used to enforce the global single verified owner rule.
    /// Only returns true for verified signing ownership, not watch-only.
    /// </summary>
    /// <param name="walletId">The wallet ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the wallet is owned by any principal with verified signing</returns>
    Task<bool> IsWalletOwnedByVerifiedSigningAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a credential already exists for any principal.
    /// Used to enforce credential uniqueness across all principals.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the credential exists for any principal</returns>
    Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all principals that have verified signing ownership of the specified wallets.
    /// Used for validation and conflict detection during Dynamic bundle processing.
    /// Only returns principals with verified signing ownership, not watch-only.
    /// This is a read-only operation that returns untracked entities.
    /// </summary>
    /// <param name="walletIds">The wallet IDs to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to their owning principals (verified signing only)</returns>
    Task<Dictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default);
}