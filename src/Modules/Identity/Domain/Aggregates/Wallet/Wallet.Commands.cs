using System.Text.RegularExpressions;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.Events;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Command methods for Wallet aggregate.
/// </summary>
public sealed partial class Wallet
{
    private AxonUserId? _currentOwnerId;
    private AccessMode? _currentAccessMode;
    private OwnershipStatus? _currentOwnershipStatus;

    [GeneratedRegex(@"^[1-9A-HJ-NP-Za-km-z]+$")]
    private static partial Regex Base58Pattern();

    /// <summary>
    /// Links the wallet to an owner with validation.
    /// </summary>
    public Result<Unit, Error> LinkToOwner(
        AxonUserId ownerId,
        AccessMode accessMode,
        OwnershipStatus status,
        Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> checkConflictingOwnershipFunc)
    {
        // Check if already linked to the same owner with same settings (idempotency)
        if (_currentOwnerId == ownerId &&
            _currentAccessMode == accessMode &&
            _currentOwnershipStatus == status)
        {
            return Result.Success<Unit, Error>(Unit.Value);
        }

        // Check for conflicting ownership (signing vs watchOnly)
        if (accessMode == AccessMode.Signing && status == OwnershipStatus.Verified)
        {
            var conflictCheck = checkConflictingOwnershipFunc(Id, AccessMode.Signing, OwnershipStatus.Verified);
            
            if (conflictCheck.IsFailure)
                return Result.Failure<Unit, Error>(conflictCheck.Error);

            if (conflictCheck.Value) // Another principal already owns this as verified+signing
                return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.AlreadyOwned());
        }

        var oldOwnerId = _currentOwnerId;

        _currentOwnerId = ownerId;
        _currentAccessMode = accessMode;
        _currentOwnershipStatus = status;

        // Update last seen timestamp
        UpdateLastSeen(DateTime.UtcNow);

        // Raise domain event
        RaiseDomainEvent(new WalletChangedEvent(
            Id,
            "ownership",
            oldOwnerId?.ToString() ?? "none",
            ownerId.ToString(),
            new Dictionary<string, string>
            {
                ["accessMode"] = accessMode.ToString(),
                ["status"] = status.ToString()
            }
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Unlinks the wallet from its current owner.
    /// </summary>
    public Result<Unit, Error> UnlinkFromOwner(AxonUserId ownerId)
    {
        if (_currentOwnerId != ownerId)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwnedByPrincipal());

        var oldOwnerId = _currentOwnerId;
        _currentOwnerId = null;
        _currentAccessMode = null;
        _currentOwnershipStatus = null;

        // Update last seen timestamp
        UpdateLastSeen(DateTime.UtcNow);

        // Raise domain event
        RaiseDomainEvent(new WalletChangedEvent(
            Id,
            "ownership",
            oldOwnerId?.ToString() ?? "none",
            "none",
            new Dictionary<string, string>()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the ownership status of the wallet.
    /// </summary>
    public Result<Unit, Error> UpdateOwnershipStatus(OwnershipStatus newStatus)
    {
        if (_currentOwnerId == null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

        // No-op guard
        if (_currentOwnershipStatus == newStatus)
            return Result.Success<Unit, Error>(Unit.Value);

        var oldStatus = _currentOwnershipStatus;
        _currentOwnershipStatus = newStatus;

        // Update last seen timestamp
        UpdateLastSeen(DateTime.UtcNow);

        // Raise domain event
        RaiseDomainEvent(new WalletChangedEvent(
            Id,
            "ownershipStatus",
            oldStatus?.ToString() ?? "none",
            newStatus.ToString(),
            new Dictionary<string, string>()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the access mode of the wallet ownership.
    /// </summary>
    public Result<Unit, Error> UpdateAccessMode(AccessMode newAccessMode)
    {
        if (_currentOwnerId == null)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Wallet.NotOwned());

        // No-op guard
        if (_currentAccessMode == newAccessMode)
            return Result.Success<Unit, Error>(Unit.Value);

        var oldMode = _currentAccessMode;
        _currentAccessMode = newAccessMode;

        // Update last seen timestamp
        UpdateLastSeen(DateTime.UtcNow);

        // Raise domain event
        RaiseDomainEvent(new WalletChangedEvent(
            Id,
            "accessMode",
            oldMode?.ToString() ?? "none",
            newAccessMode.ToString(),
            new Dictionary<string, string>()
        ));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Validates and normalizes a wallet address for the given chain.
    /// </summary>
    public static Result<string, Error> ValidateAndNormalizeAddress(string address, string chainId)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Result.Failure<string, Error>(
                Error.Validation("Address cannot be empty.", "WALLET.ADDRESS.EMPTY"));

        var trimmed = address.Trim();

        // Chain-specific validation
        switch (chainId?.ToLowerInvariant())
        {
            case "solana":
                // Solana addresses are base58 encoded and typically 32-44 chars
                if (trimmed.Length < 32 || trimmed.Length > 44)
                    return Result.Failure<string, Error>(
                        Error.Validation("Invalid Solana address length.", "WALLET.ADDRESS.INVALID_LENGTH"));
                
                // Basic character validation for base58
                var base58Pattern = Base58Pattern();
                if (!base58Pattern.IsMatch(trimmed))
                    return Result.Failure<string, Error>(
                        Error.Validation("Invalid Solana address format.", "WALLET.ADDRESS.INVALID_FORMAT"));
                break;

            case "ethereum":
            case "polygon":
            case "arbitrum":
                // EVM addresses are 42 chars (0x + 40 hex chars)
                if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || trimmed.Length != 42)
                    return Result.Failure<string, Error>(
                        Error.Validation("Invalid EVM address format.", "WALLET.ADDRESS.INVALID_FORMAT"));

                // Validate hex characters (skip "0x" prefix)
                var hexPart = trimmed.Substring(2);
                if (!hexPart.All(c => "0123456789abcdefABCDEF".Contains(c, StringComparison.Ordinal)))
                    return Result.Failure<string, Error>(
                        Error.Validation("Invalid EVM address format.", "WALLET.ADDRESS.INVALID_FORMAT"));

                // Normalize to lowercase
                trimmed = trimmed.ToLowerInvariant();
                break;

            default:
                // Generic validation for unknown chains
                if (trimmed.Length < 10 || trimmed.Length > 200)
                    return Result.Failure<string, Error>(
                        Error.Validation("Address length out of valid range.", "WALLET.ADDRESS.INVALID_LENGTH"));
                break;
        }

        return Result.Success<string, Error>(trimmed);
    }
}