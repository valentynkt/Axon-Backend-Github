using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for wallet ownership verification with transaction guards and row-level locking.
/// </summary>
public interface IWalletVerificationService
{
    /// <summary>
    /// Verifies wallet ownership with transaction-scoped row locking and automatic revocation.
    /// </summary>
    /// <param name="walletId">The wallet to verify ownership for.</param>
    /// <param name="principalId">The principal claiming ownership.</param>
    /// <param name="accessMode">The access mode for the ownership.</param>
    /// <param name="verificationSource">The source of verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the verified ownership or an error.</returns>
    Task<Result<WalletOwnership, Error>> VerifyWalletOwnershipAsync(
        WalletId walletId,
        AxonUserId principalId,
        AccessMode accessMode,
        VerificationSource verificationSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes wallet ownership with audit trail.
    /// </summary>
    /// <param name="ownershipId">The ownership to revoke.</param>
    /// <param name="reason">The reason for revocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, Error>> RevokeOwnershipAsync(
        WalletOwnershipId ownershipId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a wallet can be set as default for a principal.
    /// </summary>
    /// <param name="principalId">The principal ID.</param>
    /// <param name="walletId">The wallet ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating if the wallet can be set as default.</returns>
    Task<Result<bool, Error>> CanSetAsDefaultAsync(
        AxonUserId principalId,
        WalletId walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies multiple wallet ownerships in a single transaction with consistent lock ordering to prevent deadlocks.
    /// </summary>
    /// <param name="requests">Collection of verification requests with wallet ID, principal ID, access mode, and verification source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the list of verified ownerships or an error.</returns>
    Task<Result<IReadOnlyList<WalletOwnership>, Error>> VerifyBatchWalletOwnershipsAsync(
        IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)> requests,
        CancellationToken cancellationToken = default);
}