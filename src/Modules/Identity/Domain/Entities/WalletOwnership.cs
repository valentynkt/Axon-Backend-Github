using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Entities.Base;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Links a principal to a wallet with ownership details.
/// </summary>
public sealed class WalletOwnership :  AuditableDeletableEntity<WalletOwnershipId>
{
    public AxonUserId PrincipalId { get; private set; }
    public WalletId WalletId { get; private set; }
    public AccessMode AccessMode { get; private set; }
    public OwnershipStatus Status { get; private set; }
    public VerificationSource VerificationSource { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokeReason { get; private set; }


    // Navigation property for resolution
    public AxonPrincipal Principal { get; private set; } = null!;

    // EF Core constructor
    private WalletOwnership() { }

    private WalletOwnership(
        WalletOwnershipId id,
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode,
        OwnershipStatus status,
        VerificationSource verificationSource) : base(id)
    {
        PrincipalId = principalId;
        WalletId = walletId;
        AccessMode = accessMode;
        Status = status;
        VerificationSource = verificationSource;
    }

    public static WalletOwnership Create(
        AxonUserId principalId,
        WalletId walletId,
        AccessMode accessMode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Pending,
        VerificationSource verificationSource = VerificationSource.DynamicAttested)
    {
        return new WalletOwnership(
            WalletOwnershipId.New(),
            principalId,
            walletId,
            accessMode,
            status,
            verificationSource);
    }

    /// <summary>
    /// Updates the ownership status with validation for valid transitions.
    /// </summary>
    public Result<Unit, Error> UpdateStatus(OwnershipStatus newStatus, string? revokeReason = null)
    {
        // Validate status transition
        var transitionResult = ValidateStatusTransition(Status, newStatus);
        if (transitionResult.IsFailure)
            return transitionResult;

        // No-op guard
        if (Status == newStatus)
            return Result.Success<Unit, Error>(Unit.Value);

        Status = newStatus;

        // Update timestamps based on status
        switch (newStatus)
        {
            case OwnershipStatus.Verified:
                VerifiedAt = DateTime.UtcNow;
                RevokedAt = null;
                RevokeReason = null;
                break;
            case OwnershipStatus.Revoked:
                RevokedAt = DateTime.UtcNow;
                RevokeReason = revokeReason;
                break;
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the access mode with validation.
    /// </summary>
    public Result<Unit, Error> UpdateAccessMode(AccessMode newAccessMode)
    {
        // Cannot change access mode for revoked ownership
        if (Status == OwnershipStatus.Revoked)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.OwnershipRevoked());

        // No-op guard
        if (AccessMode == newAccessMode)
            return Result.Success<Unit, Error>(Unit.Value);

        AccessMode = newAccessMode;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Validates if a status transition is allowed.
    /// </summary>
    private static Result<Unit, Error> ValidateStatusTransition(OwnershipStatus current, OwnershipStatus target)
    {
        // Same status is always allowed (no-op)
        if (current == target)
            return Result.Success<Unit, Error>(Unit.Value);

        // Define valid transitions
        var isValidTransition = (current, target) switch
        {
            (OwnershipStatus.Pending, OwnershipStatus.Verified) => true,
            (OwnershipStatus.Pending, OwnershipStatus.Revoked) => true,
            (OwnershipStatus.Verified, OwnershipStatus.Revoked) => true,
            (OwnershipStatus.Revoked, OwnershipStatus.Verified) => true, // Re-verification allowed
            _ => false
        };

        if (!isValidTransition)
            return Result.Failure<Unit, Error>(
                Error.BusinessRule($"Invalid status transition from {current} to {target}", 
                    "WALLET.OWNERSHIP.INVALID_TRANSITION"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Checks if this ownership is verified and has signing access.
    /// </summary>
    public bool IsVerifiedSigning => Status == OwnershipStatus.Verified && AccessMode == AccessMode.Signing;

    /// <summary>
    /// Checks if this ownership can be set as a default wallet.
    /// </summary>
    public bool CanBeDefault => IsVerifiedSigning;

    /// <summary>
    /// Checks if this ownership is active (not revoked).
    /// </summary>
    public bool IsActive => Status != OwnershipStatus.Revoked;
}