using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Entities.Base;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// AxonPrincipal aggregate root representing a subject (human or service).
/// </summary>
public sealed partial class AxonPrincipal : AggregateRoot<AxonId>
{
    private readonly List<IdentityCredential> _credentials = [];
    private readonly List<WalletOwnership> _walletOwnerships = [];
    private readonly List<PrincipalChainDefault> _principalChainDefaults = [];

    public IReadOnlyCollection<IdentityCredential> Credentials => _credentials;
    public IReadOnlyCollection<WalletOwnership> WalletOwnerships => _walletOwnerships;
    public IReadOnlyCollection<PrincipalChainDefault> PrincipalChainDefaults => _principalChainDefaults;

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

    /// <summary>
    /// Creates a new human principal with Dynamic credential.
    /// Used during exchange operations to establish identity from Dynamic JWT.
    /// </summary>
    public static Result<AxonPrincipal, Error> CreateWithDynamicCredential(
        ProviderType providerType,
        string issuer,
        string subject,
        AxonId? id = null)
    {
        var principal = new AxonPrincipal(id ?? AxonId.New(), PrincipalType.Human);

        // Create Dynamic credential
        var credential = IdentityCredential.Create(
            principal.Id,
            providerType.Value,
            issuer,
            subject);

        principal._credentials.Add(credential);

        return Result.Success<AxonPrincipal, Error>(principal);
    }
}