using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Represents the result of checking wallet ownership conflicts.
/// </summary>
public abstract record OwnershipConflictResult;

/// <summary>
/// No conflict found - wallet can be linked.
/// </summary>
public sealed record NoConflict() : OwnershipConflictResult;

/// <summary>
/// Conflict found - wallet is already owned by another principal.
/// </summary>
public sealed record Conflict(AxonId ExistingOwnerPrincipalId) : OwnershipConflictResult;

/// <summary>
/// Service for enforcing wallet ownership and credential invariants.
/// Centralizes all ownership validation logic to maintain domain consistency.
/// </summary>
public interface IWalletOwnershipService
{
    /// <summary>
    /// Checks if a credential is already linked to any principal.
    /// Returns the existing principal if found, null if available for linking.
    /// </summary>
    /// <param name="providerType">The provider type of the credential</param>
    /// <param name="issuer">The credential issuer</param>
    /// <param name="subject">The credential subject</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with existing principal or null if credential is available</returns>
    Task<Result<AxonPrincipal?, Error>> CheckCredentialUniquenessAsync(
        ProviderType providerType, 
        string issuer, 
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if linking a wallet would create an ownership conflict.
    /// Returns structured conflict information for better error handling.
    /// </summary>
    /// <param name="walletId">The wallet ID to check</param>
    /// <param name="requestingPrincipalId">The principal requesting the link</param>
    /// <param name="proofType">The proof type for the ownership</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with conflict information (NoConflict or Conflict with owner ID)</returns>
    Task<Result<OwnershipConflictResult, Error>> CheckWalletOwnershipConflictAsync(
        WalletId walletId,
        AxonId requestingPrincipalId,
        ProofType proofType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates all prerequisites for wallet linking in one operation.
    /// Combines credential uniqueness and ownership conflict checks.
    /// </summary>
    /// <param name="principal">The principal attempting to link the wallet</param>
    /// <param name="walletId">The wallet ID to link</param>
    /// <param name="proofType">The proof type for the ownership</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating if wallet linking is valid</returns>
    Task<Result<bool, Error>> ValidateWalletLinkingAsync(
        AxonPrincipal principal,
        WalletId walletId,
        ProofType proofType,
        CancellationToken cancellationToken = default);
}