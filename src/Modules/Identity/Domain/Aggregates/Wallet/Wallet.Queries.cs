using Axon.Modules.Identity.Domain.Enums;

namespace Axon.Modules.Identity.Domain.Aggregates.Wallet;

/// <summary>
/// Query methods for Wallet aggregate.
/// </summary>
public sealed partial class Wallet
{
    /// <summary>
    /// Gets the current owner ID of the wallet.
    /// </summary>
    public AxonUserId? CurrentOwnerId => _currentOwnerId;

    /// <summary>
    /// Gets the current access mode of the wallet ownership.
    /// </summary>
    public AccessMode? CurrentAccessMode => _currentAccessMode;

    /// <summary>
    /// Gets the current ownership status of the wallet.
    /// </summary>
    public OwnershipStatus? CurrentOwnershipStatus => _currentOwnershipStatus;

    /// <summary>
    /// Checks if the wallet can be linked to a new owner.
    /// </summary>
    public bool CanBeLinked(AccessMode requestedAccessMode, OwnershipStatus requestedStatus)
    {
        // If not currently owned, it can be linked
        if (_currentOwnerId == null)
            return true;

        // If current ownership is not verified+signing, new ownership can coexist
        if (_currentAccessMode != AccessMode.Signing || _currentOwnershipStatus != OwnershipStatus.Verified)
            return true;

        // If requested is not verified+signing, it can coexist with current
        if (requestedAccessMode != AccessMode.Signing || requestedStatus != OwnershipStatus.Verified)
            return true;

        // Both are verified+signing - conflict
        return false;
    }

    /// <summary>
    /// Checks if the wallet is currently owned by a specific principal.
    /// </summary>
    public bool IsOwnedBy(AxonUserId principalId)
    {
        return _currentOwnerId == principalId;
    }

    /// <summary>
    /// Checks if the wallet has verified signing ownership.
    /// </summary>
    public bool HasVerifiedSigningOwnership()
    {
        return _currentAccessMode == AccessMode.Signing && 
               _currentOwnershipStatus == OwnershipStatus.Verified;
    }

    /// <summary>
    /// Checks if the wallet is currently owned (by anyone).
    /// </summary>
    public bool IsOwned => _currentOwnerId != null;

    /// <summary>
    /// Gets the wallet coordinates (chain + address).
    /// </summary>
    public (string ChainId, string Address) GetCoordinates()
    {
        return (ChainId, Address.Value);
    }

    /// <summary>
    /// Checks if the wallet matches the given chain.
    /// </summary>
    public bool IsOnChain(string chainId)
    {
        return string.Equals(ChainId, chainId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the age of the wallet since first seen.
    /// </summary>
    public TimeSpan GetAge()
    {
        return DateTime.UtcNow - FirstSeenAt;
    }

    /// <summary>
    /// Gets the time since last activity.
    /// </summary>
    public TimeSpan GetTimeSinceLastSeen()
    {
        return DateTime.UtcNow - LastSeenAt;
    }

    /// <summary>
    /// Validates if the wallet can be set as a default for a principal.
    /// </summary>
    public bool CanBeSetAsDefault(AxonUserId principalId)
    {
        return IsOwnedBy(principalId) && HasVerifiedSigningOwnership();
    }
}