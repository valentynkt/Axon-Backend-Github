using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Maps a principal's default wallet per blockchain.
/// </summary>
public sealed class PrincipalChainDefault :  AuditableDeletableEntity<Guid>
{
    public AxonId PrincipalId { get; private set; }
    public string ChainId { get; private set; } = string.Empty;
    public WalletId WalletId { get; private set; }

    // EF Core constructor
    private PrincipalChainDefault() { }

    private PrincipalChainDefault(AxonId principalId, string chainId, WalletId walletId) : base(Guid.NewGuid())
    {
        PrincipalId = principalId;
        ChainId = chainId;
        WalletId = walletId;
    }

    public static PrincipalChainDefault Create(AxonId principalId, string chainId, WalletId walletId)
    {
        return new PrincipalChainDefault(principalId, chainId, walletId);
    }

    public void UpdateWallet(WalletId walletId)
    {
        WalletId = walletId;
    }
}