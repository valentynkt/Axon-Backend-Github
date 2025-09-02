using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Abstractions;

/// <summary>
/// Application service responsible for enforcing cross-principal business rules 
/// that cannot be enforced within a single aggregate boundary.
/// Coordinates with repository to validate global constraints.
/// </summary>
public interface IWalletOwnershipService
{
    /// <summary>
    /// Validates that a wallet can be linked to a principal.
    /// Enforces global single verified owner rule (P2).
    /// </summary>
    /// <param name="walletId">The wallet to link</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if the wallet can be linked</returns>
    Task<Result<Unit, Error>> ValidateWalletCanBeLinkedAsync(
        WalletId walletId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that a credential can be linked to a principal.
    /// Enforces credential uniqueness across all principals (E2).
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating if the credential can be linked</returns>
    Task<Result<Unit, Error>> ValidateCredentialCanBeLinkedAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default);

    /// <summary>
    /// Finds the principal that currently owns a wallet.
    /// Used for conflict resolution and ownership transfers.
    /// </summary>
    /// <param name="walletId">The wallet to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The owning principal if found, null otherwise</returns>
    Task<AxonPrincipal?> FindWalletOwnerAsync(
        WalletId walletId,
        CancellationToken ct = default);

    /// <summary>
    /// Finds the principal that owns a specific credential.
    /// Used for credential conflict resolution.
    /// </summary>
    /// <param name="providerType">The identity provider type</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The principal that owns the credential, null if not found</returns>
    Task<AxonPrincipal?> FindCredentialOwnerAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken ct = default);

    /// <summary>
    /// Validates multiple wallets for bulk linking operations.
    /// Used in Dynamic bundle processing to validate all wallets before linking.
    /// </summary>
    /// <param name="walletIds">The wallets to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping wallet IDs to their current owners, if any</returns>
    Task<IReadOnlyDictionary<WalletId, AxonPrincipal>> ValidateWalletsForBulkLinkingAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken ct = default);
}