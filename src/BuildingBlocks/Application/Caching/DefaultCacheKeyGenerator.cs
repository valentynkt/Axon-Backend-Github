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
    private readonly IContentHasher _contentHasher;
    private readonly ICacheKeyBuilder _keyBuilder;

    public DefaultCacheKeyGenerator(
        IOptions<CacheOptions> options,
        IContentHasher contentHasher,
        ICacheKeyBuilder keyBuilder)
    {
        _options = options?.Value ?? new();
        _contentHasher = contentHasher ?? throw new ArgumentNullException(nameof(contentHasher));
        _keyBuilder = keyBuilder ?? throw new ArgumentNullException(nameof(keyBuilder));
    }

    public string GenerateKey<TQuery>(TQuery query) where TQuery : notnull
{
    // Check if this is an IQuery with declarative caching properties
    if (query is IAxonRequest axonRequest)
    {
        return GenerateQueryKey(axonRequest, query);
    }
    
    // Fallback for non-IQuery requests (backward compatibility)
    return GenerateFallbackKey(query);
}

    private string GenerateQueryKey<TQuery>(IAxonRequest axonRequest, TQuery queryInstance)
{
    // Try to get cache key prefix from query interface or use type name
    var prefix = GetCacheKeyPrefix(queryInstance);
    var contentHash = _contentHasher.ComputeHash(queryInstance);
    
    // Optional trace context isolation
    var contextPart = ShouldIncludeTraceContext(axonRequest) 
        ? GetTraceContextPart(axonRequest.TraceId)
        : "global";
        
    return _keyBuilder.BuildQueryKey(prefix, contentHash, contextPart);
}

    private string GenerateFallbackKey<TQuery>(TQuery query)
    {
        var typeName = typeof(TQuery).Name;
        var contentHash = _contentHasher.ComputeHash(query);
        
        return _keyBuilder.BuildFallbackKey(typeName, contentHash);
    }

    private bool ShouldIncludeTraceContext(IAxonRequest axonRequest)
{
    return _options.IncludeTraceInKey && !string.IsNullOrEmpty(axonRequest.TraceId);
}

    private static string GetTraceContextPart(string? traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return "global";
            
        // Use first 8 characters of trace ID for brevity while maintaining uniqueness
        return traceId.Length >= 8 ? traceId[..8] : traceId;
    }

    private static string GetCacheKeyPrefix<TQuery>(TQuery queryInstance)
{
    // Use reflection to check if this implements any IQuery interface
    var queryType = typeof(TQuery);
    var queryInterfaces = queryType.GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>))
        .FirstOrDefault();
        
    if (queryInterfaces != null)
    {
        // Get the CacheKeyPrefix property from the interface
        var property = queryInterfaces.GetProperty("CacheKeyPrefix");
        if (property?.GetValue(queryInstance) is string prefix && !string.IsNullOrEmpty(prefix))
        {
            return prefix;
        }
    }
    
    // Fallback to type name
    return queryType.Name;
}


}