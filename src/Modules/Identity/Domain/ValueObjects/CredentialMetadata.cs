using System.Collections.Immutable;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing structured metadata for identity credentials.
/// Provides type-safe access to common metadata fields while allowing extension.
/// </summary>
public sealed class CredentialMetadata : IEquatable<CredentialMetadata>
{
    private readonly IReadOnlyDictionary<string, object> _value;

    public static readonly CredentialMetadata Empty = new(ImmutableDictionary<string, object>.Empty);

    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "session_public_key",
        "email_hash", 
        "verification_method",
        "device_id",
        "ip_address",
        "user_agent",
        "timestamp"
    };

    private CredentialMetadata(IReadOnlyDictionary<string, object> value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates CredentialMetadata from a dictionary.
    /// </summary>
    public static Result<CredentialMetadata, Error> Create(Dictionary<string, object>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
            return Result.Success<CredentialMetadata, Error>(Empty);

        var validationResult = ValidateInput(metadata);
        if (validationResult.IsFailure)
            return Result.Failure<CredentialMetadata, Error>(validationResult.Error);

        var normalized = NormalizeInput(metadata);
        return Result.Success<CredentialMetadata, Error>(new CredentialMetadata(normalized));
    }

    private static Result<Unit, Error> ValidateInput(Dictionary<string, object> input)
    {
        if (input.Count > 20)
            return Result.Failure<Unit, Error>(
                Error.Validation("Credential metadata cannot contain more than 20 entries.", "IDENTITY.CREDENTIAL.METADATA.TOO_MANY_ENTRIES"));

        foreach (var kvp in input)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                return Result.Failure<Unit, Error>(
                    Error.Validation("Metadata key cannot be empty.", "IDENTITY.CREDENTIAL.METADATA.KEY.EMPTY"));

            if (kvp.Key.Length > 100)
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Metadata key '{kvp.Key}' exceeds 100 characters.", "IDENTITY.CREDENTIAL.METADATA.KEY.TOO_LONG"));

            if (kvp.Value is string strValue && strValue.Length > 1000)
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Metadata value for key '{kvp.Key}' exceeds 1000 characters.", "IDENTITY.CREDENTIAL.METADATA.VALUE.TOO_LONG"));

            // Validate reserved key formats
            if (string.Equals(kvp.Key, "email_hash", StringComparison.OrdinalIgnoreCase))
            {
                if (kvp.Value is not string emailHash || emailHash.Length != 64)
                    return Result.Failure<Unit, Error>(
                        Error.Validation("email_hash must be a 64-character hexadecimal string.", "IDENTITY.CREDENTIAL.METADATA.EMAIL_HASH.INVALID"));
            }

            if (string.Equals(kvp.Key, "session_public_key", StringComparison.OrdinalIgnoreCase))
            {
                if (kvp.Value is not string publicKey || string.IsNullOrWhiteSpace(publicKey))
                    return Result.Failure<Unit, Error>(
                        Error.Validation("session_public_key must be a non-empty string.", "IDENTITY.CREDENTIAL.METADATA.SESSION_KEY.INVALID"));
            }
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static ImmutableDictionary<string, object> NormalizeInput(Dictionary<string, object> input)
    {
        return input.ToImmutableDictionary(
            kvp => ReservedKeys.Contains(kvp.Key) ? kvp.Key.ToLowerInvariant() : kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new CredentialMetadata with an additional key-value pair.
    /// </summary>
    public Result<CredentialMetadata, Error> WithMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure<CredentialMetadata, Error>(
                Error.Validation("Metadata key cannot be empty.", "IDENTITY.CREDENTIAL.METADATA.KEY.EMPTY"));

        var newMetadata = _value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        newMetadata[key] = value;

        return Create(newMetadata);
    }

    /// <summary>
    /// Creates a new CredentialMetadata with a key removed.
    /// </summary>
    public CredentialMetadata WithoutMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_value.ContainsKey(key))
            return this;

        var newMetadata = _value
            .Where(kvp => !string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new CredentialMetadata(newMetadata);
    }

    /// <summary>
    /// Gets the session public key if present.
    /// </summary>
    public string? GetSessionPublicKey()
    {
        return _value.TryGetValue("session_public_key", out var value) ? value as string : null;
    }

    /// <summary>
    /// Gets the email hash if present.
    /// </summary>
    public string? GetEmailHash()
    {
        return _value.TryGetValue("email_hash", out var value) ? value as string : null;
    }

    /// <summary>
    /// Gets the verification method if present.
    /// </summary>
    public string? GetVerificationMethod()
    {
        return _value.TryGetValue("verification_method", out var value) ? value as string : null;
    }

    /// <summary>
    /// Gets a metadata value by key.
    /// </summary>
    public T? GetValue<T>(string key) where T : class
    {
        return _value.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// Checks if a metadata key exists.
    /// </summary>
    public bool ContainsKey(string key)
    {
        return _value.ContainsKey(key);
    }

    /// <summary>
    /// Gets all metadata keys.
    /// </summary>
    public IEnumerable<string> GetKeys()
    {
        return _value.Keys;
    }

    /// <summary>
    /// Gets the number of metadata entries.
    /// </summary>
    public int Count => _value.Count;

    /// <summary>
    /// Checks if metadata is empty.
    /// </summary>
    public bool IsEmpty => _value.Count == 0;

    /// <summary>
    /// Gets the underlying dictionary for serialization purposes.
    /// </summary>
    public IReadOnlyDictionary<string, object> Value => _value;

    public bool Equals(CredentialMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (_value.Count != other._value.Count) return false;

        foreach (var kvp in _value)
        {
            if (!other._value.TryGetValue(kvp.Key, out var otherValue) || !Equals(kvp.Value, otherValue))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as CredentialMetadata);

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

    public static bool operator ==(CredentialMetadata? left, CredentialMetadata? right) => Equals(left, right);
    public static bool operator !=(CredentialMetadata? left, CredentialMetadata? right) => !Equals(left, right);
}