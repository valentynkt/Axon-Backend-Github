using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Implementation of IValidationContext providing metadata-aware validation capabilities.
/// Extracts and provides safe access to request metadata, trace context, and feature flags
/// for Epic 04 enhanced validation integration.
/// </summary>
public sealed class ValidationContext : IValidationContext
{
    private readonly Lazy<IReadOnlyDictionary<string, bool>> _featureFlags;
    
    public Guid RequestId { get; }
    public DateTime RequestedAt { get; }
    public string? TraceId { get; }
    public string? SpanId { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
    
    public IReadOnlyDictionary<string, bool> FeatureFlags => _featureFlags.Value;
    
    /// <summary>
    /// Creates a validation context from an Axon request.
    /// Captures all relevant metadata and trace information for validation use.
    /// </summary>
    /// <param name="request">The request to create context from</param>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    public ValidationContext(IAxonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        RequestId = request.RequestId;
        RequestedAt = request.RequestedAt;
        TraceId = request.TraceId;
        SpanId = request.SpanId;
        Metadata = request.Metadata;
        
        // Lazy initialization of feature flags to avoid parsing cost unless needed
        _featureFlags = new Lazy<IReadOnlyDictionary<string, bool>>(ExtractFeatureFlags);
    }
    
    /// <summary>
    /// Safely retrieve typed metadata value by key.
    /// Handles missing keys and type conversion gracefully.
    /// </summary>
    /// <typeparam name="T">Expected type of the metadata value</typeparam>
    /// <param name="key">Metadata key to retrieve</param>
    /// <returns>Typed metadata value or default(T)</returns>
    public T? GetMetadata<T>(string key)
    {
        if (string.IsNullOrEmpty(key) || !Metadata.TryGetValue(key, out var value))
        {
            return default;
        }
        
        try
        {
            // Handle direct type matches
            if (value is T directMatch)
            {
                return directMatch;
            }
            
            // Handle string to other type conversions
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.ToString()!;
            }
            
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (underlyingType != null)
            {
                if (value == null)
                {
                    return default;
                }
                
                return (T?)Convert.ChangeType(value, underlyingType);
            }
            
            // Handle enum types
            if (typeof(T).IsEnum)
            {
                var stringValue = value.ToString();
                if (Enum.TryParse(typeof(T), stringValue, true, out var enumValue))
                {
                    return (T)enumValue;
                }
            }
            
            // Handle primitive type conversions
            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            
            // For complex types, return default
            return default;
        }
        catch
        {
            // Swallow conversion exceptions and return default
            // This ensures validation continues even with malformed metadata
            return default;
        }
    }
    
    /// <summary>
    /// Check if a specific feature flag is enabled.
    /// Safely handles missing or invalid feature flag values.
    /// </summary>
    /// <param name="featureName">Name of the feature flag to check</param>
    /// <returns>True if feature is enabled, false otherwise</returns>
    public bool IsFeatureEnabled(string featureName)
    {
        if (string.IsNullOrEmpty(featureName))
        {
            return false;
        }
        
        return FeatureFlags.TryGetValue(featureName, out var isEnabled) && isEnabled;
    }
    
    /// <summary>
    /// Extracts feature flags from metadata.
    /// Supports both nested FeatureFlags dictionary and flattened flag entries.
    /// </summary>
    /// <returns>Dictionary of feature flag names and their states</returns>
    private IReadOnlyDictionary<string, bool> ExtractFeatureFlags()
    {
        var flags = new Dictionary<string, bool>();
        
        // First, check for explicit FeatureFlags metadata entry
        if (Metadata.TryGetValue("FeatureFlags", out var featureFlagsValue))
        {
            switch (featureFlagsValue)
            {
                case IReadOnlyDictionary<string, bool> flagDict:
                    foreach (var (key, value) in flagDict)
                    {
                        flags[key] = value;
                    }
                    break;
                    
                case IDictionary<string, object> objectDict:
                    foreach (var (key, value) in objectDict)
                    {
                        if (TryConvertToBoolean(value, out var boolValue))
                        {
                            flags[key] = boolValue;
                        }
                    }
                    break;
            }
        }
        
        // Second, scan for individual feature flag entries with "Feature_" prefix
        foreach (var (key, value) in Metadata)
        {
            if (key.StartsWith("Feature_", StringComparison.OrdinalIgnoreCase))
            {
                var featureName = key.Substring(8); // Remove "Feature_" prefix
                if (TryConvertToBoolean(value, out var boolValue))
                {
                    flags[featureName] = boolValue;
                }
            }
        }
        
        return flags;
    }
    
    /// <summary>
    /// Safely converts various value types to boolean.
    /// Supports common boolean representations (true/false, 1/0, yes/no, etc.)
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Converted boolean value</param>
    /// <returns>True if conversion successful</returns>
    private static bool TryConvertToBoolean(object? value, out bool result)
    {
        result = false;
        
        if (value == null)
        {
            return false;
        }
        
        return value switch
        {
            bool boolValue => SetResult(boolValue),
            string stringValue => bool.TryParse(stringValue, out result) ||
                                 TryParseBooleanString(stringValue, out result),
            int intValue => SetResult(intValue != 0),
            long longValue => SetResult(longValue != 0),
            double doubleValue => SetResult(Math.Abs(doubleValue) > double.Epsilon),
            _ => false
        };
        
        bool SetResult(bool value)
        {
            result = value;
            return true;
        }
    }
    
    /// <summary>
    /// Parse common boolean string representations.
    /// Supports: yes/no, on/off, enabled/disabled, active/inactive
    /// </summary>
    private static bool TryParseBooleanString(string value, out bool result)
    {
        result = false;
        
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        
        var normalized = value.Trim().ToLowerInvariant();
        
        switch (normalized)
        {
            case "yes":
            case "y":
            case "on":
            case "enabled":
            case "active":
            case "1":
                result = true;
                return true;
                
            case "no":
            case "n":
            case "off":
            case "disabled":
            case "inactive":
            case "0":
                result = false;
                return true;
                
            default:
                return false;
        }
    }
}