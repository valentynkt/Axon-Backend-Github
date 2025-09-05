using Axon.Modules.Identity.Domain.ValueObjects;
using AxonEntity = BuildingBlocks.Core.Domain.Entities.Base.AuditableEntity<BuildingBlocks.Primitives.Ids.AxonId>;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Principal profile owned entity containing user preferences and settings.
/// Owned by AxonPrincipal aggregate - can only be modified through the aggregate.
/// Chain defaults are now managed directly by AxonPrincipal via PrincipalChainDefault collection.
/// </summary>
public sealed class PrincipalProfile : AxonEntity
{
    public PreferredLanguage PreferredLanguage { get; private set; }
    public RiskTier RiskTier { get; private set; }

    // EF Core parameterless constructor
    private PrincipalProfile() : base() 
    {
        PreferredLanguage = PreferredLanguage.Default;
        RiskTier = RiskTier.Default;
    }

    private PrincipalProfile(
        AxonId axonId,
        PreferredLanguage preferredLanguage,
        RiskTier riskTier) : base(axonId)
    {
        PreferredLanguage = preferredLanguage;
        RiskTier = riskTier;
    }

    /// <summary>
    /// Creates a default profile for the specified principal.
    /// </summary>
    internal static PrincipalProfile CreateDefault(AxonId axonId)
    {
        return new PrincipalProfile(
            axonId,
            PreferredLanguage.Default,
            RiskTier.Default);
    }

    /// <summary>
    /// Updates the preferred language setting.
    /// </summary>
    internal Result<Unit, Error> UpdatePreferredLanguage(PreferredLanguage newLanguage)
    {
        if (PreferredLanguage.Value == newLanguage.Value)
            return Result.Success<Unit, Error>(Unit.Value);

        PreferredLanguage = newLanguage;
        MarkUpdated();
        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Updates the risk tier setting.
    /// </summary>
    internal Result<Unit, Error> UpdateRiskTier(RiskTier newRiskTier)
    {
        if (RiskTier.Value == newRiskTier.Value)
            return Result.Success<Unit, Error>(Unit.Value);

        RiskTier = newRiskTier;
        MarkUpdated();
        return Result.Success<Unit, Error>(Unit.Value);
    }
}