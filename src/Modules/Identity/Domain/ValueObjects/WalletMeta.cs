using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Non-authoritative wallet metadata annotations.
/// JSON-like map with size and shape constraints.
/// Used for wallet app names, provider hints, etc.
/// </summary>
public sealed class WalletMeta
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Dictionary<string, object> _data;
    
    public const int MaxSizeBytes = 4096; // 4KB limit
    public const int MaxKeyLength = 100;
    public const int MaxValueLength = 1000;
    public const int MaxKeys = 50;

    public IReadOnlyDictionary<string, object> Data => new ReadOnlyDictionary<string, object>(_data);

    private WalletMeta(Dictionary<string, object> data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public static readonly WalletMeta Empty = new(new Dictionary<string, object>());

    /// <summary>
    /// Creates WalletMeta from a dictionary with validation.
    /// </summary>
    public static Result<WalletMeta, Error> Create(Dictionary<string, object>? data = null)
    {
        if (data is null || data.Count == 0)
            return Result.Success<WalletMeta, Error>(Empty);

        var validationResult = ValidateMetadata(data);
        if (validationResult.IsFailure)
            return Result.Failure<WalletMeta, Error>(validationResult.Error);

        // Create defensive copy
        var sanitizedData = new Dictionary<string, object>();
        foreach (var kvp in data)
        {
            sanitizedData[kvp.Key.Trim()] = SanitizeValue(kvp.Value);
        }

        return Result.Success<WalletMeta, Error>(new WalletMeta(sanitizedData));
    }

    /// <summary>
    /// Creates WalletMeta from JSON string.
    /// </summary>
    public static Result<WalletMeta, Error> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Result.Success<WalletMeta, Error>(Empty);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, SerializerOptions);
            return Create(data);
        }
        catch (JsonException ex)
        {
            return Result.Failure<WalletMeta, Error>(
                Error.Validation($"Invalid JSON format: {ex.Message}", "WALLET.META.INVALID_JSON"));
        }
    }

    /// <summary>
    /// Merges this metadata with additional metadata.
    /// New values overwrite existing keys. Operation is idempotent and additive.
    /// </summary>
    public Result<WalletMeta, Error> Merge(Dictionary<string, object> additionalData)
    {
        if (additionalData is null || additionalData.Count == 0)
            return Result.Success<WalletMeta, Error>(this);

        var merged = new Dictionary<string, object>(_data);
        
        foreach (var kvp in additionalData)
        {
            var key = kvp.Key.Trim();
            if (string.IsNullOrEmpty(key)) continue;
            
            merged[key] = SanitizeValue(kvp.Value);
        }

        return Create(merged);
    }

    /// <summary>
    /// Gets a value by key with type conversion.
    /// </summary>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !_data.TryGetValue(key.Trim(), out var value))
            return defaultValue;

        try
        {
            if (value is T typedValue)
                return typedValue;

            // Attempt conversion
            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString()!;

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Serializes to JSON string.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_data, SerializerOptions);
    }

    /// <summary>
    /// Gets the estimated size in bytes.
    /// </summary>
    public int EstimatedSizeBytes => Encoding.UTF8.GetByteCount(ToJson());

    public bool IsEmpty => _data.Count == 0;

    private static Result<Unit, Error> ValidateMetadata(Dictionary<string, object> data)
    {
        if (data.Count > MaxKeys)
            return Result.Failure<Unit, Error>(
                Error.Validation($"Too many metadata keys. Maximum {MaxKeys} allowed.", "WALLET.META.TOO_MANY_KEYS"));

        foreach (var kvp in data)
        {
            // Validate key
            if (string.IsNullOrWhiteSpace(kvp.Key))
                return Result.Failure<Unit, Error>(
                    Error.Validation("Metadata key cannot be empty.", "WALLET.META.EMPTY_KEY"));

            if (kvp.Key.Length > MaxKeyLength)
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Metadata key '{kvp.Key}' exceeds maximum length of {MaxKeyLength}.", "WALLET.META.KEY_TOO_LONG"));

            if (!IsValidKeyFormat(kvp.Key))
                return Result.Failure<Unit, Error>(
                    Error.Validation($"Metadata key '{kvp.Key}' contains invalid characters. Use alphanumeric, underscore, or hyphen only.", "WALLET.META.INVALID_KEY_FORMAT"));

            // Validate value
            var validValueResult = ValidateValue(kvp.Key, kvp.Value);
            if (validValueResult.IsFailure)
                return validValueResult;
        }

        // Validate total size
        var tempMeta = new WalletMeta(data);
        if (tempMeta.EstimatedSizeBytes > MaxSizeBytes)
            return Result.Failure<Unit, Error>(
                Error.Validation($"Metadata size ({tempMeta.EstimatedSizeBytes} bytes) exceeds maximum of {MaxSizeBytes} bytes.", "WALLET.META.TOO_LARGE"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static Result<Unit, Error> ValidateValue(string key, object value)
    {
        if (value is null)
            return Result.Success<Unit, Error>(Unit.Value);

        var valueString = value.ToString() ?? string.Empty;
        if (valueString.Length > MaxValueLength)
            return Result.Failure<Unit, Error>(
                Error.Validation($"Value for key '{key}' exceeds maximum length of {MaxValueLength}.", "WALLET.META.VALUE_TOO_LONG"));

        // Only allow primitive types
        var valueType = value.GetType();
        if (!IsSupportedValueType(valueType))
            return Result.Failure<Unit, Error>(
                Error.Validation($"Unsupported value type '{valueType.Name}' for key '{key}'. Use string, number, or boolean only.", "WALLET.META.UNSUPPORTED_VALUE_TYPE"));

        return Result.Success<Unit, Error>(Unit.Value);
    }

    private static bool IsValidKeyFormat(string key)
    {
        // Allow alphanumeric, underscore, and hyphen
        return key.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }

    private static bool IsSupportedValueType(Type type)
    {
        return type == typeof(string) ||
               type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(double) ||
               type == typeof(decimal) ||
               type == typeof(bool) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset);
    }

    private static object SanitizeValue(object value)
    {
        return value switch
        {
            string str => str.Trim(),
            _ => value
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is not WalletMeta other) return false;
        if (_data.Count != other._data.Count) return false;

        foreach (var kvp in _data)
        {
            if (!other._data.TryGetValue(kvp.Key, out var otherValue) ||
                !Equals(kvp.Value, otherValue))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _data.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}