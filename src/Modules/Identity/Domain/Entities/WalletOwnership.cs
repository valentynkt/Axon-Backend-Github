using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Links a principal to a wallet with ownership details.
/// </summary>
public sealed class WalletOwnership : global::BuildingBlocks.Core.Domain.Entities.Base.Entity<WalletOwnershipId>
{
    public AxonId PrincipalId { get; private set; }
    public WalletId WalletId { get; private set; }
    public AccessMode AccessMode { get; private set; }
    public OwnershipStatus Status { get; private set; }

    // EF Core constructor
    private WalletOwnership() { }

    private WalletOwnership(WalletOwnershipId id, AxonId principalId, WalletId walletId, AccessMode accessMode, OwnershipStatus status) : base(id)
    {
        PrincipalId = principalId;
        WalletId = walletId;
        AccessMode = accessMode;
        Status = status;
    }

    public static WalletOwnership Create(AxonId principalId, WalletId walletId, AccessMode accessMode = AccessMode.Signing, OwnershipStatus status = OwnershipStatus.Pending)
    {
        return new WalletOwnership(WalletOwnershipId.New(), principalId, walletId, accessMode, status);
    }

    public void UpdateStatus(OwnershipStatus status)
    {
        Status = status;
    }

    public void UpdateAccessMode(AccessMode accessMode)
    {
        AccessMode = accessMode;
    }

    public bool IsVerifiedSigning => Status == OwnershipStatus.Verified && AccessMode == AccessMode.Signing;
}