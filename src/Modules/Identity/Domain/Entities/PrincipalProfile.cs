using System.Text.Json;
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
    /// JSON map of chain -> WalletId for per-chain default wallet selection.
    /// Enforces single default per chain constraint.
    /// </summary>
    public string DefaultPerChainJson { get; private set; } = "{}";

    private Dictionary<string, long>? _defaultPerChainCache;
    private bool _cacheInitialized;

    public IReadOnlyDictionary<string, long> DefaultPerChain
    {
        get
        {
            if (!_cacheInitialized)
            {
                try
                {
                    _defaultPerChainCache = JsonSerializer.Deserialize<Dictionary<string, long>>(DefaultPerChainJson) ?? new();
                    _cacheInitialized = true;
                }
                catch (JsonException)
                {
                    // If deserialization fails, use empty dictionary and reset JSON
                    _defaultPerChainCache = new Dictionary<string, long>();
                    DefaultPerChainJson = "{}";
                    _cacheInitialized = true;
                }
            }
            return _defaultPerChainCache ?? new Dictionary<string, long>();
        }
    }

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
        _defaultPerChainCache = new Dictionary<string, long>();
        DefaultPerChainJson = "{}";
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

    internal Result<Unit, Error> SetDefaultWalletForChain(string chain, long walletId)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return Result.Failure<Unit, Error>(
                Error.Validation("Chain cannot be empty.", "IDENTITY.PROFILE.CHAIN.EMPTY"));

        if (walletId <= 0)
            return Result.Failure<Unit, Error>(
                Error.Validation("Wallet ID must be positive.", "IDENTITY.PROFILE.WALLET_ID.INVALID"));

        var defaults = DefaultPerChain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        defaults[chain] = walletId;
        
        return UpdateDefaultPerChain(defaults);
    }

    internal Result<Unit, Error> ClearDefaultForChain(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return Result.Success<Unit, Error>(Unit.Value);

        var defaults = DefaultPerChain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        if (defaults.Remove(chain))
        {
            return UpdateDefaultPerChain(defaults);
        }
        
        return Result.Success<Unit, Error>(Unit.Value);
    }

    internal long? GetDefaultWalletForChain(string chain)
    {
        return string.IsNullOrWhiteSpace(chain) ? null : DefaultPerChain.GetValueOrDefault(chain, 0) is var id && id > 0 ? id : null;
    }

    internal bool HasDefaultWalletForChain(string chain)
    {
        return GetDefaultWalletForChain(chain).HasValue;
    }

    private Result<Unit, Error> UpdateDefaultPerChain(Dictionary<string, long> newDefaults)
    {
        try
        {
            _defaultPerChainCache = newDefaults;
            DefaultPerChainJson = JsonSerializer.Serialize(newDefaults);
            _cacheInitialized = true;
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (JsonException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Internal($"Failed to serialize chain defaults: {ex.Message}", "IDENTITY.PROFILE.SERIALIZATION.FAILED"));
        }
    }

    internal Result<Unit, Error> InitializeDefaultForChainIfEmpty(string chain, long walletId)
    {
        if (HasDefaultWalletForChain(chain))
            return Result.Success<Unit, Error>(Unit.Value);

        return SetDefaultWalletForChain(chain, walletId);
    }
}