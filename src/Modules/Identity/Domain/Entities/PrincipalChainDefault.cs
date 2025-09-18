using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Maps a principal's default wallet per blockchain.
/// </summary>
public sealed class PrincipalChainDefault :  AuditableDeletableEntity<Guid>
{
    public AxonUserId PrincipalId { get; private set; }
    public string ChainId { get; private set; } = string.Empty;
    public WalletId WalletId { get; private set; }

    // EF Core constructor
    private PrincipalChainDefault() { }

    private PrincipalChainDefault(AxonUserId principalId, string chainId, WalletId walletId) : base(Guid.CreateVersion7())
    {
        PrincipalId = principalId;
        ChainId = chainId;
        WalletId = walletId;
    }

    public static PrincipalChainDefault Create(AxonUserId principalId, string chainId, WalletId walletId)
    {
        return new PrincipalChainDefault(principalId, chainId, walletId);
    }

    public void UpdateWallet(WalletId walletId)
    {
        WalletId = walletId;
    }
}