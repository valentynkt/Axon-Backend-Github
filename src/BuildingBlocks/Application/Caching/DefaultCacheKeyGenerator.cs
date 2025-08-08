using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Abstractions.CQRS;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Default implementation of cache key generator for Epic 04 Story 02.
/// Generates deterministic keys using query properties, content hash, and optional trace context.
/// </summary>
public sealed class DefaultCacheKeyGenerator : ICacheKeyGenerator
{
    private readonly CacheOptions _options;
    
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DefaultCacheKeyGenerator(IOptions<CacheOptions> options)
    {
        _options = options?.Value ?? new CacheOptions();
    }

    public string GenerateKey<TQuery>(TQuery query) where TQuery : notnull
    {
        // Check if this is an IQuery with declarative caching properties
        if (query is IQuery<object> typedQuery)
        {
            return GenerateQueryKey(typedQuery, query);
        }
        
        // Fallback for non-IQuery requests (backward compatibility)
        return GenerateFallbackKey(query);
    }

    private string GenerateQueryKey<TQuery>(IQuery<object> query, TQuery queryInstance)
    {
        var prefix = query.CacheKeyPrefix;
        var contentHash = ComputeContentHash(queryInstance);
        
        // Optional trace context isolation
        var contextPart = ShouldIncludeTraceContext(query) 
            ? GetTraceContextPart(query.TraceId)
            : "global";
            
        // Format: axon:query:{prefix}:{contentHash}:{contextPart}
        return $"axon:query:{prefix}:{contentHash}:{contextPart}";
    }

    private static string GenerateFallbackKey<TQuery>(TQuery query)
    {
        var typeName = typeof(TQuery).Name;
        var contentHash = ComputeContentHash(query);
        
        // Format: axon:cache:{typeName}:v1:{contentHash}
        return $"axon:cache:{typeName}:v1:{contentHash}";
    }

    private bool ShouldIncludeTraceContext(IQuery<object> query)
    {
        return _options.IncludeTraceInKey && !string.IsNullOrEmpty(query.TraceId);
    }

    private static string GetTraceContextPart(string? traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return "global";
            
        // Use first 8 characters of trace ID for brevity while maintaining uniqueness
        return traceId.Length >= 8 ? traceId[..8] : traceId;
    }

    private static string ComputeContentHash<T>(T content)
    {
        try
        {
            // Serialize the content to JSON for consistent hashing
            var json = JsonSerializer.Serialize(content, SerializerOptions);
            var inputBytes = Encoding.UTF8.GetBytes(json);
            
            // Use SHA256 for cryptographic strength and collision resistance
            var hashBytes = SHA256.HashData(inputBytes);
            
            // Convert to hex string and take first 16 characters for brevity
            return Convert.ToHexString(hashBytes)[..16];
        }
        catch (Exception)
        {
            // Fallback for non-serializable objects
            // Use GetHashCode with string formatting for consistency
            return Math.Abs(content.GetHashCode()).ToString("x8");
        }
    }
}