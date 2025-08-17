using System.Diagnostics;
using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Application.Validation.Core;

/// <summary>
/// Immutable implementation of <see cref="IValidationContext"/> providing
/// safe access to metadata, trace context, and feature flags.
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
    /// Build a context from a canonical Axon request.
    /// </summary>
    public ValidationContext(IAxonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequestId   = request.RequestId;
        RequestedAt = request.RequestedAt;
        TraceId     = request.TraceId;
        SpanId      = request.SpanId;
        Metadata    = CloneMetadata(request.Metadata);

        _featureFlags = new Lazy<IReadOnlyDictionary<string, bool>>(ExtractFeatureFlags);
    }

    /// <summary>
    /// Build a context from primitives/ambient values (when no IAxonRequest is available).
    /// </summary>
    public ValidationContext(
        Guid? requestId = null,
        DateTime? requestedAtUtc = null,
        string? traceId = null,
        string? spanId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        RequestId   = requestId ?? Guid.NewGuid();
        RequestedAt = requestedAtUtc ?? DateTime.UtcNow;

        // Prefer explicit trace/span; otherwise read from current Activity if present
        TraceId = traceId ?? Activity.Current?.TraceId.ToString();
        SpanId  = spanId  ?? Activity.Current?.SpanId.ToString();

        Metadata = CloneMetadata(metadata);

        _featureFlags = new Lazy<IReadOnlyDictionary<string, bool>>(ExtractFeatureFlags);
    }

    /// <summary>
    /// Create a new context with additional/overridden metadata (immutably).
    /// </summary>
    public ValidationContext WithMetadata(IReadOnlyDictionary<string, object> extra)
    {
        var merged = new Dictionary<string, object>(Metadata, StringComparer.OrdinalIgnoreCase);
        if (extra != null)
        {
            foreach (var kv in extra)
                merged[kv.Key] = kv.Value;
        }
        return new ValidationContext(RequestId, RequestedAt, TraceId, SpanId, merged);
    }

    public T? GetMetadata<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !Metadata.TryGetValue(key, out var value))
            return default;

        try
        {
            // Direct match
            if (value is T t) return t;

            // Strings
            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString()!;

            // Nullable<T>
            var underlying = Nullable.GetUnderlyingType(typeof(T));
            if (underlying != null)
            {
                if (value is null) return default;
                return (T?)Convert.ChangeType(value, underlying);
            }

            // Enum
            if (typeof(T).IsEnum && value is not null)
            {
                var s = value.ToString();
                if (Enum.TryParse(typeof(T), s, true, out var e))
                    return (T)e;
            }

            // Primitive/decimal
            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
            {
                var converted = Convert.ChangeType(value, typeof(T));
                return converted is not null ? (T)converted : default;
            }

            // Fallback
            return default;
        }
        catch
        {
            // Never throw from context accessors
            return default;
        }
    }

    public bool IsFeatureEnabled(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName)) return false;
        return FeatureFlags.TryGetValue(featureName, out var enabled) && enabled;
    }

    // ---- helpers ----

    private static Dictionary<string, object> CloneMetadata(IReadOnlyDictionary<string, object>? source)
    {
        if (source is null || source.Count == 0)
            return new Dictionary<string, object>(0);

        return new Dictionary<string, object>(source, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, bool> ExtractFeatureFlags()
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // 1) Nested FeatureFlags object
        if (Metadata.TryGetValue("FeatureFlags", out var ff))
        {
            switch (ff)
            {
                case IReadOnlyDictionary<string, bool> typed:
                    foreach (var (k, v) in typed) flags[k] = v;
                    break;

                case IDictionary<string, object> objDict:
                    foreach (var (k, v) in objDict)
                        if (TryToBool(v, out var b)) flags[k] = b;
                    break;
            }
        }

        // 2) Flattened entries with "Feature_" prefix
        foreach (var (k, v) in Metadata)
        {
            if (!k.StartsWith("Feature_", StringComparison.OrdinalIgnoreCase)) continue;
            var name = k.Substring("Feature_".Length);
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (TryToBool(v, out var b)) flags[name] = b;
        }

        return flags;
    }

    private static bool TryToBool(object? value, out bool result)
    {
        result = false;
        if (value is null) return false;

        switch (value)
        {
            case bool b:
                result = b; return true;

            case string s:
                if (bool.TryParse(s, out result)) return true;
                var n = s.Trim().ToLowerInvariant();
                if (n is "yes" or "y" or "on" or "enabled" or "active" or "1") { result = true;  return true; }
                if (n is "no"  or "n" or "off" or "disabled" or "inactive" or "0") { result = false; return true; }
                return false;

            case int i:
                result = i != 0; return true;

            case long l:
                result = l != 0; return true;

            case double d:
                result = Math.Abs(d) > double.Epsilon; return true;

            default:
                return false;
        }
    }
}
