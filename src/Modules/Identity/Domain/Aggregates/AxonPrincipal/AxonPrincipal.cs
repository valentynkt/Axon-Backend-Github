using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// AxonPrincipal aggregate root representing a subject (human or service).
/// </summary>
public sealed partial class AxonPrincipal : AggregateRoot<AxonId>
{
    private readonly List<IdentityCredential> _credentials = [];
    private readonly List<WalletOwnership> _walletOwnerships = [];

    public IReadOnlyCollection<IdentityCredential> Credentials => _credentials;
    public IReadOnlyCollection<WalletOwnership> WalletOwnerships => _walletOwnerships;

    public PrincipalType Type { get; private set; }
    public RiskTier RiskTier { get; private set; } = RiskTier.Low;

    // EF Core constructor
    private AxonPrincipal() { }

    private AxonPrincipal(AxonId id, PrincipalType type) : base(id)
    {
        Type = type;
        RiskTier = RiskTier.Low;
    }

    public static AxonPrincipal CreateHuman(AxonId? id = null)
    {
        return new AxonPrincipal(id ?? AxonId.New(), PrincipalType.Human);
    }

    public static AxonPrincipal CreateService(AxonId? id = null)
    {
        return new AxonPrincipal(id ?? AxonId.New(), PrincipalType.Service);
    }
}