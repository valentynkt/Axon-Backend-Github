using BuildingBlocks.Core.Domain.Entities.Base;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Maps a principal's default wallet per blockchain.
/// ChainId now contains compound format (e.g., "solana-mainnet") with all network information.
/// This is an owned entity that belongs to the AxonPrincipal aggregate.
/// </summary>
public sealed class PrincipalChainDefault : OwnedAuditableEntity
{
    // Id property is kept as a regular property (not from base class)
    // since owned entities don't have independent identity
    public Guid Id { get; private set; }
    public AxonUserId PrincipalId { get; private set; }
    public string ChainId { get; private set; } = string.Empty;
    public WalletId WalletId { get; private set; }


    // EF Core constructor
    private PrincipalChainDefault() { }

    private PrincipalChainDefault(AxonUserId principalId, string chainId, WalletId walletId) : base()
    {
        Id = Guid.CreateVersion7();
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