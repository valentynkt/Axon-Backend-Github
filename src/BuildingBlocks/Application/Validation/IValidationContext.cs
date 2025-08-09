namespace BuildingBlocks.Application.Validation;

/// <summary>
/// Provides metadata-aware validation context for Epic 04 enhanced validation integration.
/// Enables validators to access request metadata, trace context, and feature flags for
/// sophisticated context-aware validation rules.
/// </summary>
public interface IValidationContext
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// Enables error correlation and request tracking.
    /// </summary>
    Guid RequestId { get; }
    
    /// <summary>
    /// Timestamp when the request was created.
    /// Useful for time-based validation rules and audit trails.
    /// </summary>
    DateTime RequestedAt { get; }
    
    /// <summary>
    /// W3C TraceContext trace identifier.
    /// Enables distributed tracing correlation in validation errors.
    /// </summary>
    string? TraceId { get; }
    
    /// <summary>
    /// W3C TraceContext span identifier.
    /// Enables fine-grained tracing of validation operations.
    /// </summary>
    string? SpanId { get; }
    
    /// <summary>
    /// Immutable metadata dictionary providing access to all request context.
    /// Contains tenant information, user context, feature flags, and custom properties.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// Tenant identifier extracted from metadata.
    /// Returns null if not present, enabling multi-tenant validation scenarios.
    /// </summary>
    string? TenantId => GetMetadata<string>("TenantId");
    
    /// <summary>
    /// User identifier extracted from metadata.
    /// Returns null if not present, enabling user-specific validation rules.
    /// </summary>
    string? UserId => GetMetadata<string>("UserId");
    
    /// <summary>
    /// Feature flags dictionary extracted from metadata.
    /// Enables feature-flag conditional validation rules.
    /// </summary>
    IReadOnlyDictionary<string, bool> FeatureFlags { get; }
    
    /// <summary>
    /// Safely retrieve typed metadata value by key.
    /// Returns null if key doesn't exist or value cannot be cast to T.
    /// </summary>
    /// <typeparam name="T">Expected type of the metadata value</typeparam>
    /// <param name="key">Metadata key to retrieve</param>
    /// <returns>Typed metadata value or null</returns>
    T? GetMetadata<T>(string key);
    
    /// <summary>
    /// Check if a specific feature flag is enabled.
    /// Safely handles missing or invalid feature flag values.
    /// </summary>
    /// <param name="featureName">Name of the feature flag to check</param>
    /// <returns>True if feature is enabled, false otherwise</returns>
    bool IsFeatureEnabled(string featureName);
}