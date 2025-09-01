using Axon.Modules.Identity.Domain.ValueObjects;
using AxonEntity = BuildingBlocks.Core.Domain.Entities.Base.Entity<BuildingBlocks.Primitives.Ids.AxonId>;

namespace Axon.Modules.Identity.Domain.Entities;

/// <summary>
/// Principal profile owned entity containing user preferences and settings.
/// Owned by AxonPrincipal aggregate - can only be modified through the aggregate.
/// </summary>
public sealed class PrincipalProfile : AxonEntity
{
    public PreferredLanguage PreferredLanguage { get; private set; }
    public RiskTier RiskTier { get; private set; }
    
    /// <summary>
    /// Chain-to-wallet mappings for per-chain default wallet selection.
    /// Enforces single default per chain constraint through value object.
    /// </summary>
    public ChainDefaults DefaultPerChain { get; private set; }

    // EF Core parameterless constructor
    private PrincipalProfile() : base() 
    {
        PreferredLanguage = PreferredLanguage.Default;
        RiskTier = RiskTier.Default;
        DefaultPerChain = ChainDefaults.Empty;
    }

    private PrincipalProfile(
        AxonId axonId,
        PreferredLanguage preferredLanguage,
        RiskTier riskTier) : base(axonId)
    {
        PreferredLanguage = preferredLanguage;
        RiskTier = riskTier;
        DefaultPerChain = ChainDefaults.Empty;
    }

    internal static PrincipalProfile CreateDefault(AxonId axonId)
    {
        return new PrincipalProfile(
            axonId,
            PreferredLanguage.Default,
            RiskTier.Default);
    }

    internal Result<Unit, Error> UpdatePreferredLanguage(PreferredLanguage newLanguage)
    {
        if (PreferredLanguage.Value == newLanguage.Value)
            return Result.Success<Unit, Error>(Unit.Value);

        PreferredLanguage = newLanguage;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> UpdateRiskTier(RiskTier newRiskTier)
    {
        if (RiskTier.Value == newRiskTier.Value)
            return Result.Success<Unit, Error>(Unit.Value);

        RiskTier = newRiskTier;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> SetDefaultWalletForChain(string chain, WalletId walletId)
    {
        var result = DefaultPerChain.WithDefault(chain, walletId);
        if (result.IsFailure)
            return Result.Failure<Unit, Error>(result.Error);

        DefaultPerChain = result.Value;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> ClearDefaultForChain(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return Result.Success<Unit, Error>(Unit.Value);

        DefaultPerChain = DefaultPerChain.WithoutDefault(chain);
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal WalletId? GetDefaultWalletForChain(string chain)
    {
        return DefaultPerChain.GetDefaultWalletForChain(chain);
    }

    internal bool HasDefaultWalletForChain(string chain)
    {
        return DefaultPerChain.HasDefaultForChain(chain);
    }

    internal Result<Unit, Error> InitializeDefaultForChainIfEmpty(string chain, WalletId walletId)
    {
        if (HasDefaultWalletForChain(chain))
            return Result.Success<Unit, Error>(Unit.Value);

        return SetDefaultWalletForChain(chain, walletId);
    }
}