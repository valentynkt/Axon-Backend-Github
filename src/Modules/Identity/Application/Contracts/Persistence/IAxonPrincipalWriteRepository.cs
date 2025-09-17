using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Write repository contract for AxonPrincipal aggregate persistence operations.
/// </summary>
public interface IAxonPrincipalWriteRepository : IWriteRepository<AxonPrincipal, AxonId>
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
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the credential is already taken, false otherwise</returns>
    Task<bool> IsCredentialTakenAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default);
}