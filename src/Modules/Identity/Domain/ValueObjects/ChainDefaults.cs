using System.Collections.Immutable;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing chain-to-wallet-ID mappings for default wallet selection.
/// Enforces single default per chain constraint at the value object level.
/// </summary>
public sealed class ChainDefaults : IEquatable<ChainDefaults>
{
    private readonly IReadOnlyDictionary<string, long> _value;

    public static readonly ChainDefaults Empty = new(ImmutableDictionary<string, long>.Empty);

    private ChainDefaults(IReadOnlyDictionary<string, long> value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates ChainDefaults from a dictionary of chain-to-wallet mappings.
    /// </summary>
    public static Result<ChainDefaults, Error> Create(Dictionary<string, long>? defaults)
    {
        if (defaults is null || defaults.Count == 0)
            return Result.Success<ChainDefaults, Error>(Empty);

        var validationResult = ValidateInput(defaults);
        if (validationResult.IsFailure)
            return Result.Failure<ChainDefaults, Error>(validationResult.Error);

        var normalized = NormalizeInput(defaults);
        return Result.Success<ChainDefaults, Error>(new ChainDefaults(normalized));
    }

    private static Result<Unit, Error> ValidateInput(Dictionary<string, long> input)
    {
        foreach (var kvp in input)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                return Result.Failure<Unit, Error>(
                    Error.Validation("Chain identifier cannot be empty.", "IDENTITY.PROFILE.CHAIN.EMPTY"));

            if (kvp.Value <= 0)
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Wallet ID must be positive for chain '{kvp.Key}'.", "IDENTITY.PROFILE.WALLET_ID.INVALID"));

            if (kvp.Key.Length > 50)
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Chain identifier '{kvp.Key}' exceeds 50 characters.", "IDENTITY.PROFILE.CHAIN.TOO_LONG"));
        }

        if (input.Count > 20)
            return Result.Failure<Unit, Error>(
                Error.Validation("Cannot have defaults for more than 20 chains.", "IDENTITY.PROFILE.CHAIN_DEFAULTS.TOO_MANY"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static ImmutableDictionary<string, long> NormalizeInput(Dictionary<string, long> input)
    {
        return input.ToImmutableDictionary(
            kvp => kvp.Key.ToLowerInvariant(),
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new ChainDefaults with an additional chain-wallet mapping.
    /// </summary>
    public Result<ChainDefaults, Error> WithDefault(string chain, long walletId)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return Result.Failure<ChainDefaults, Error>(
                Error.Validation("Chain cannot be empty.", "IDENTITY.PROFILE.CHAIN.EMPTY"));

        if (walletId <= 0)
            return Result.Failure<ChainDefaults, Error>(
                Error.Validation("Wallet ID must be positive.", "IDENTITY.PROFILE.WALLET_ID.INVALID"));

        var newDefaults = _value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        newDefaults[chain.ToLowerInvariant()] = walletId;

        return Create(newDefaults);
    }

    /// <summary>
    /// Creates a new ChainDefaults with a chain-wallet mapping removed.
    /// </summary>
    public ChainDefaults WithoutDefault(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return this;

        var normalizedChain = chain.ToLowerInvariant();
        if (!_value.ContainsKey(normalizedChain))
            return this;

        var newDefaults = _value
            .Where(kvp => !string.Equals(kvp.Key, normalizedChain, StringComparison.OrdinalIgnoreCase))
            .ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new ChainDefaults(newDefaults);
    }

    /// <summary>
    /// Gets the default wallet ID for a specific chain.
    /// </summary>
    public long? GetDefaultWalletForChain(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
            return null;

        var normalizedChain = chain.ToLowerInvariant();
        return _value.TryGetValue(normalizedChain, out var walletId) && walletId > 0 ? walletId : null;
    }

    /// <summary>
    /// Checks if there's a default wallet configured for the specified chain.
    /// </summary>
    public bool HasDefaultForChain(string chain)
    {
        return GetDefaultWalletForChain(chain).HasValue;
    }

    /// <summary>
    /// Gets all chains that have defaults configured.
    /// </summary>
    public IEnumerable<string> GetChainsWithDefaults()
    {
        return _value.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key);
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
    public IReadOnlyDictionary<string, long> Value => _value;

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
        foreach (var kvp in _value.OrderBy(k => k.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(ChainDefaults? left, ChainDefaults? right) => Equals(left, right);
    public static bool operator !=(ChainDefaults? left, ChainDefaults? right) => !Equals(left, right);
}