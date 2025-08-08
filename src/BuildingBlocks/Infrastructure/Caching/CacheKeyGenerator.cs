using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Caching;


/// <summary>
/// Generates deterministic cache keys for declarative query caching in Epic 04 Story 02.
/// Supports W3C TraceContext integration and uses query declarative properties.
/// </summary>
public interface ICacheKeyGenerator
{
    string GenerateKey<TQuery>(TQuery query) where TQuery : notnull;
}

public sealed class CacheKeyGenerator : ICacheKeyGenerator
{
    private readonly CacheOptions _options;
    
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public CacheKeyGenerator(IOptions<CacheOptions> options)
    {
        _options = options?.Value ?? new CacheOptions();
    }

    public string GenerateKey<TQuery>(TQuery query) where TQuery : notnull
    {
        if (query is BuildingBlocks.Core.CQRS.IQuery<object> typedQuery)
        {
            var prefix = typedQuery.CacheKeyPrefix;
            var queryHash = ComputeQueryHash(query);
            
            // Optional trace context isolation based on configuration
            var contextPart = _options.IncludeTraceInKey && !string.IsNullOrEmpty(typedQuery.TraceId)
                ? typedQuery.TraceId[..8] // Use first 8 chars of trace ID for brevity
                : "global";
                
            return $"axon:query:{prefix}:{queryHash}:{contextPart}";
        }
        
        // Fallback for non-IQuery requests
        var requestType = typeof(TQuery);
        var typeName = requestType.Name;
        var requestHash = ComputeQueryHash(query);
        
        return $"axon:cache:{typeName}:v1:{requestHash}";
    }

    private static string ComputeQueryHash<T>(T query)
    {
        try
        {
            var json = JsonSerializer.Serialize(query, SerializerOptions);
            var inputBytes = Encoding.UTF8.GetBytes(json);
            var hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes)[..16]; // Use first 16 characters for brevity
        }
        catch
        {
            // Fallback for non-serializable objects
            return query.GetHashCode().ToString("x8");
        }
    }
}

/// <summary>
/// Cache configuration for requests.
/// </summary>
public sealed class CacheConfiguration
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);
    public bool UseSlidingExpiration { get; init; } = false;
    public TimeSpan L1Duration { get; init; } = TimeSpan.FromSeconds(30);
    public string[] Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Interface for requests that want to configure their own caching.
/// </summary>
public interface ICacheable
{
    CacheConfiguration GetCacheConfiguration();
}

/// <summary>
/// Attribute for declarative cache configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheableAttribute : Attribute
{
    public int DurationSeconds { get; init; } = 300;  // 5 minutes default
    public bool UseSlidingExpiration { get; init; } = false;
    public int L1DurationSeconds { get; init; } = 30;  // 30 seconds L1
    public string[]? Tags { get; init; }
}

/// <summary>
/// Provides cache configuration for request types.
/// </summary>
public interface ICacheConfigurationProvider
{
    CacheConfiguration? GetConfiguration(string requestTypeName);
}

public sealed class CacheConfigurationProvider : ICacheConfigurationProvider
{
    private readonly Dictionary<string, CacheConfiguration> _configurations;

    public CacheConfigurationProvider()
    {
        _configurations = InitializeDefaultConfigurations();
    }

    public CacheConfiguration? GetConfiguration(string requestTypeName)
    {
        return _configurations.TryGetValue(requestTypeName, out var config) ? config : null;
    }

    private Dictionary<string, CacheConfiguration> InitializeDefaultConfigurations()
    {
        return new Dictionary<string, CacheConfiguration>
        {
            ["GetUserByIdQuery"] = new CacheConfiguration
            {
                Duration = TimeSpan.FromMinutes(10),
                L1Duration = TimeSpan.FromMinutes(1),
                Tags = new[] { "users", "user-{UserId}" }
            },
            ["GetUserProfileQuery"] = new CacheConfiguration
            {
                Duration = TimeSpan.FromMinutes(15),
                UseSlidingExpiration = true,
                L1Duration = TimeSpan.FromMinutes(2),
                Tags = new[] { "users", "profiles" }
            }
        };
    }
}

/// <summary>
/// Cached entry wrapper with metadata.
/// </summary>
public sealed class CachedEntry<T>
{
    public T Value { get; init; } = default!;
    public DateTimeOffset CachedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string Version { get; init; } = "1.0";

    public bool IsExpired() => DateTimeOffset.UtcNow > ExpiresAt;
}

/// <summary>
/// Configuration options for caching behavior.
/// </summary>
/// <summary>
/// Configuration options for Epic 04 Story 02 caching behavior.
/// Controls default cache durations, providers, and W3C context inclusion.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Default cache duration when not specified by query.
    /// </summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Whether to include W3C TraceContext in cache keys for isolation.
    /// </summary>
    public bool IncludeTraceInKey { get; set; } = false;
    
    /// <summary>
    /// Compression threshold for distributed cache entries (in bytes).
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024; // 1KB
    
    /// <summary>
    /// Redis connection string for distributed caching.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";
    
    /// <summary>
    /// Redis instance name for key prefixing.
    /// </summary>
    public string InstanceName { get; set; } = "axon";
    
    /// <summary>
    /// Redis database number to use.
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;
    
    /// <summary>
    /// Memory cache size limit in MB.
    /// </summary>
    public int MemoryCacheSizeLimitMB { get; set; } = 100;
}