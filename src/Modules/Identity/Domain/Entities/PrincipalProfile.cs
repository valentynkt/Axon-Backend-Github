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

    internal Result<Unit, Error> SetDefaultWalletForChain(ChainId chainId, WalletId walletId)
    {
        var result = DefaultPerChain.WithDefault(chainId, walletId);
        if (result.IsFailure)
            return Result.Failure<Unit, Error>(result.Error);

        DefaultPerChain = result.Value;
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal Result<Unit, Error> ClearDefaultForChain(ChainId chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId.Value))
            return Result.Success<Unit, Error>(Unit.Value);

        DefaultPerChain = DefaultPerChain.WithoutDefault(chainId);
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal WalletId? GetDefaultWalletForChain(ChainId chainId)
    {
        return DefaultPerChain.GetDefaultWalletForChain(chainId);
    }

    internal bool HasDefaultWalletForChain(ChainId chainId)
    {
        return DefaultPerChain.HasDefaultForChain(chainId);
    }

    internal Result<Unit, Error> InitializeDefaultForChainIfEmpty(ChainId chainId, WalletId walletId)
    {
        if (HasDefaultWalletForChain(chainId))
            return Result.Success<Unit, Error>(Unit.Value);

        return SetDefaultWalletForChain(chainId, walletId);
    }

    internal void ClearDefaultsReferencing(WalletId walletId)
    {
        var chains = DefaultPerChain.Value
            .Where(kvp => kvp.Value == walletId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var chainId in chains)
        {
            DefaultPerChain = DefaultPerChain.WithoutDefault(chainId);
        }
    }
}