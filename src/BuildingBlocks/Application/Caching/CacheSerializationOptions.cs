using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Centralized JSON serialization options for cache storage.
/// Provides consistent serialization across L1/L2 cache layers.
/// </summary>
public static class CacheSerializationOptions
{
    /// <summary>
    /// JSON serializer options optimized for cache storage.
    /// - Consistent property naming (camelCase)
    /// - Ignores null values to reduce size
    /// - No indentation for compact storage
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = null // Disable reflection-based serialization for AOT compatibility
    };
}