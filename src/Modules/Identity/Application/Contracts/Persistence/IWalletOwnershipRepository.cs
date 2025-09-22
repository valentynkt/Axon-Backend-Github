using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Contracts.Persistence;

/// <summary>
/// Repository for wallet ownership operations supporting deterministic principal resolution.
/// </summary>
public interface IWalletOwnershipRepository
{
    /// <summary>
    /// Finds active ownerships (status != revoked) for a specific wallet.
    /// Used in wallet-fallback resolution to find candidate principals.
    /// </summary>
    Task<IReadOnlyList<WalletOwnership>> FindActiveOwnershipsByWalletAsync(
        WalletId walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new wallet ownership relationship.
    /// </summary>
    Task<WalletOwnership> CreateOwnershipAsync(
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status,
        VerificationSource verificationSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an ownership with the specified reason.
    /// Used for auto-revoke during tie-breaking.
    /// </summary>
    Task RevokeOwnershipAsync(
        WalletOwnershipId ownershipId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds ownerships by wallet IDs for batch operations.
    /// Returns verified+signing ownerships grouped by wallet ID.
    /// </summary>
    Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> FindVerifiedSigningOwnersAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds verified+signing ownership for a wallet across all network environments.
    /// Used for cross-network-environment conflict detection in principal resolution.
    /// </summary>
    Task<WalletOwnership?> FindVerifiedSigningOwnershipAcrossEnvironmentsAsync(
        string chainId,
        Address address,
        CancellationToken cancellationToken = default);
}