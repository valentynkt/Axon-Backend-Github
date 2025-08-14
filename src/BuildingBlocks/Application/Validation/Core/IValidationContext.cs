namespace BuildingBlocks.Application.Validation.Core;

/// <summary>
/// Provides metadata-aware validation context.
/// Enables validators to access request metadata, trace context, and feature flags
/// without becoming stateful.
/// </summary>
public interface IValidationContext
{
    /// <summary>Unique identifier for this request instance.</summary>
    Guid RequestId { get; }

    /// <summary>Timestamp when the request was created (UTC).</summary>
    DateTime RequestedAt { get; }

    /// <summary>W3C TraceContext trace identifier.</summary>
    string? TraceId { get; }

    /// <summary>W3C TraceContext span identifier.</summary>
    string? SpanId { get; }

    /// <summary>
    /// Immutable metadata dictionary providing access to request context
    /// (tenant, user, feature flags, custom properties).
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Feature flags extracted from metadata (either a nested FeatureFlags object
    /// or flattened keys with "Feature_" prefix).
    /// </summary>
    IReadOnlyDictionary<string, bool> FeatureFlags { get; }

    /// <summary>Convenience accessors (derived via metadata).</summary>
    string? TenantId => GetMetadata<string>("TenantId");
    string? UserId   => GetMetadata<string>("UserId");

    /// <summary>
    /// Safely retrieve a typed metadata value by key.
    /// Returns default(T) if key doesn't exist or value cannot be converted.
    /// </summary>
    T? GetMetadata<T>(string key);

    /// <summary>
    /// Check if a specific feature flag is enabled.
    /// </summary>
    bool IsFeatureEnabled(string featureName);
}