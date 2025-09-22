using BuildingBlocks.Core.Domain.Entities.Base;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Maps a principal's default wallet per blockchain with network environment isolation.
/// </summary>
public sealed class PrincipalChainDefault :  AuditableDeletableEntity<Guid>
{
    public AxonUserId PrincipalId { get; private set; }
    public NetworkEnvironment NetworkEnvironment { get; private set; }
    public string ChainId { get; private set; } = string.Empty;
    public WalletId WalletId { get; private set; }

    // EF Core constructor
    private PrincipalChainDefault() { }

    private PrincipalChainDefault(AxonUserId principalId, NetworkEnvironment networkEnvironment, string chainId, WalletId walletId) : base(Guid.CreateVersion7())
    {
        PrincipalId = principalId;
        NetworkEnvironment = networkEnvironment;
        ChainId = chainId;
        WalletId = walletId;
    }

    public static PrincipalChainDefault Create(AxonUserId principalId, NetworkEnvironment networkEnvironment, string chainId, WalletId walletId)
    {
        return new PrincipalChainDefault(principalId, networkEnvironment, chainId, walletId);
    }

    public void UpdateWallet(WalletId walletId)
    {
        WalletId = walletId;
    }
}