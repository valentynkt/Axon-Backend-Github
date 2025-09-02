using System.Collections.Immutable;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing chain-to-wallet-ID mappings for default wallet selection.
/// Enforces single default per chain constraint at the value object level.
/// </summary>
public sealed class ChainDefaults : IEquatable<ChainDefaults>
{
    private readonly IReadOnlyDictionary<ChainId, WalletId> _value;

    public static readonly ChainDefaults Empty = new(ImmutableDictionary<ChainId, WalletId>.Empty);

    private ChainDefaults(IReadOnlyDictionary<ChainId, WalletId> value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates ChainDefaults from a dictionary of chain-to-wallet mappings.
    /// </summary>
    public static Result<ChainDefaults, Error> Create(Dictionary<ChainId, WalletId>? defaults)
    {
        if (defaults is null || defaults.Count == 0)
            return Result.Success<ChainDefaults, Error>(Empty);

        var validationResult = ValidateInput(defaults);
        if (validationResult.IsFailure)
            return Result.Failure<ChainDefaults, Error>(validationResult.Error);

        var normalized = NormalizeInput(defaults);
        return Result.Success<ChainDefaults, Error>(new ChainDefaults(normalized));
    }

    private static Result<Unit, Error> ValidateInput(Dictionary<ChainId, WalletId> input)
    {
        foreach (var kvp in input)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key.Value))
                return Result.Failure<Unit, Error>(
                    Error.Validation("Chain identifier cannot be empty.", "IDENTITY.PROFILE.CHAIN.EMPTY"));

            if (kvp.Value == default(WalletId))
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Wallet ID must be positive for chain '{kvp.Key.Value}'.", "IDENTITY.PROFILE.WALLET_ID.INVALID"));
        }

        if (input.Count > 20)
            return Result.Failure<Unit, Error>(
                Error.Validation("Cannot have defaults for more than 20 chains.", "IDENTITY.PROFILE.CHAIN_DEFAULTS.TOO_MANY"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static ImmutableDictionary<ChainId, WalletId> NormalizeInput(Dictionary<ChainId, WalletId> input)
    {
        return input.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);
    }

    /// <summary>
    /// Creates a new ChainDefaults with an additional chain-wallet mapping.
    /// </summary>
    public Result<ChainDefaults, Error> WithDefault(ChainId chainId, WalletId walletId)
    {
        if (string.IsNullOrWhiteSpace(chainId.Value))
            return Result.Failure<ChainDefaults, Error>(
                Error.Validation("Chain cannot be empty.", "IDENTITY.PROFILE.CHAIN.EMPTY"));

        if (walletId == default(WalletId))
            return Result.Failure<ChainDefaults, Error>(
                Error.Validation("Wallet ID must be positive.", "IDENTITY.PROFILE.WALLET_ID.INVALID"));

        var newDefaults = _value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        newDefaults[chainId] = walletId;

        return Create(newDefaults);
    }

    /// <summary>
    /// Creates a new ChainDefaults with a chain-wallet mapping removed.
    /// </summary>
    public ChainDefaults WithoutDefault(ChainId chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId.Value))
            return this;

        if (!_value.ContainsKey(chainId))
            return this;

        var newDefaults = _value
            .Where(kvp => kvp.Key != chainId)
            .ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new ChainDefaults(newDefaults);
    }

    /// <summary>
    /// Gets the default wallet ID for a specific chain.
    /// </summary>
    public WalletId? GetDefaultWalletForChain(ChainId chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId.Value))
            return null;

        return _value.TryGetValue(chainId, out var walletId) && walletId != default(WalletId) ? walletId : null;
    }

    /// <summary>
    /// Checks if there's a default wallet configured for the specified chain.
    /// </summary>
    public bool HasDefaultForChain(ChainId chainId)
    {
        return GetDefaultWalletForChain(chainId).HasValue;
    }

    /// <summary>
    /// Gets all chains that have defaults configured.
    /// </summary>
    public IEnumerable<ChainId> GetChainsWithDefaults()
    {
        return _value.Where(kvp => kvp.Value != default(WalletId)).Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Gets the number of configured chain defaults.
    /// </summary>
    public int Count => _value.Count;

    /// <summary>
    /// Checks if any defaults are configured.
    /// </summary>
    public bool IsEmpty => _value.Count == 0;

    /// <summary>
    /// Gets the underlying dictionary for serialization purposes.
    /// </summary>
    public IReadOnlyDictionary<ChainId, WalletId> Value => _value;

    public bool Equals(ChainDefaults? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (_value.Count != other._value.Count) return false;

        foreach (var kvp in _value)
        {
            if (!other._value.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ChainDefaults);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _value.OrderBy(k => k.Key.Value, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(ChainDefaults? left, ChainDefaults? right) => Equals(left, right);
    public static bool operator !=(ChainDefaults? left, ChainDefaults? right) => !Equals(left, right);
}