using Axon.Modules.Identity.Application.Abstractions;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Application service responsible for enforcing cross-principal business rules 
/// that cannot be enforced within a single aggregate boundary.
/// Coordinates with repository to validate global constraints.
/// </summary>
public sealed class WalletOwnershipService : IWalletOwnershipService
{
    private readonly IAxonPrincipalRepository _principalRepository;

    public WalletOwnershipService(IAxonPrincipalRepository principalRepository)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
    }

    /// <summary>
    /// Validates that a wallet can be linked to a principal.
    /// Enforces global single verified owner rule (P2).
    /// </summary>
    /// <param name="walletId">The wallet to link</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if the wallet can be linked</returns>
    public async Task<Result<Unit, Error>> ValidateWalletCanBeLinkedAsync(
        WalletId walletId,
        CancellationToken ct = default)
    {
        // Check if wallet is already owned by any principal with verified signing
        var isOwned = await _principalRepository.IsWalletOwnedByVerifiedSigningAsync(walletId, ct);
        if (isOwned)
        {
            return Result.Failure<Unit, Error>(
                IdentityDomainErrors.Wallet.AlreadyOwned());
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Validates that a credential can be linked to a principal.
    /// Enforces credential uniqueness across all principals (E2).
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if the credential can be linked</returns>
    public async Task<Result<Unit, Error>> ValidateCredentialCanBeLinkedAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        var isTaken = await _principalRepository.IsCredentialTakenAsync(
            providerType, issuer, subject, ct);

        if (isTaken)
        {
            return Result.Failure<Unit, Error>(
                IdentityDomainErrors.Credential.Duplicate());
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Finds the principal that currently owns a wallet.
    /// Used for conflict resolution and ownership transfers.
    /// </summary>
    /// <param name="walletId">The wallet to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The owning principal if found, null otherwise</returns>
    public async Task<AxonPrincipal?> FindWalletOwnerAsync(
        WalletId walletId,
        CancellationToken ct = default)
    {
        return await _principalRepository.FindByWalletIdAsync(walletId, ct);
    }

    /// <summary>
    /// Finds the principal that owns a specific credential.
    /// Used for credential conflict resolution.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The principal that owns the credential, null if not found</returns>
    public async Task<AxonPrincipal?> FindCredentialOwnerAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default)
    {
        return await _principalRepository.FindByCredentialAsync(providerType, issuer, subject, ct);
    }

    /// <summary>
    /// Validates multiple wallets for bulk linking operations.
    /// Used in Dynamic bundle processing to validate all wallets before linking.
    /// </summary>
    /// <param name="walletIds">The wallets to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to their current owners, if any</returns>
    public async Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> ValidateWalletsForBulkLinkingAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default)
    {
        var owners = await _principalRepository.FindVerifiedSigningOwnersAsync(walletIds, ct);
        
        // Return the conflicts - wallets that are already owned
        return owners;
    }

    /// <summary>
    /// Creates and publishes a wallet ownership conflict event.
    /// Should be called when wallet linking is skipped due to existing ownership.
    /// Note: This method should be called from the application layer, not domain aggregates.
    /// </summary>
    /// <param name="requestedByPrincipalId">Principal attempting to link the wallet</param>
    /// <param name="existingOwnerPrincipalId">Principal that currently owns the wallet</param>
    /// <param name="walletId">The conflicted wallet ID</param>
    /// <param name="conflictReason">Reason for the conflict</param>
    /// <param name="resolutionStrategy">How the conflict was resolved</param>
    /// <param name="timeProvider">Time provider for consistent timestamps</param>
    /// <returns>The conflict event that was created</returns>
    public static WalletOwnershipConflictSkippedEvent CreateConflictEvent(
        AxonId requestedByPrincipalId,
        AxonId existingOwnerPrincipalId,
        WalletId walletId,
        string conflictReason,
        string? resolutionStrategy = null,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? TimeProvider.System;
        var now = effectiveTimeProvider.GetUtcNow();

        return new WalletOwnershipConflictSkippedEvent(
            requestedByPrincipalId, existingOwnerPrincipalId, walletId,
            conflictReason, now, resolutionStrategy);
    }
}