using System.Text.Json;
using System.Text.Json.Serialization;

namespace Axon.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Consistent JSON serialization options for database storage.
/// Provides secure, deterministic serialization for metadata and configuration fields.
/// </summary>
internal static class JsonSerializationOptions
{
    /// <summary>
    /// Standard options for database storage - secure, consistent, and deterministic.
    /// </summary>
    public static readonly JsonSerializerOptions DatabaseStorage = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
        // Security: Prevent potential RCE
        MaxDepth = 32,
        // Performance: Reduce memory allocations
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace
    };
    
    /// <summary>
    /// Options for simple value collections (like tags, lists).
    /// </summary>
    public static readonly JsonSerializerOptions SimpleCollections = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16
    };
}