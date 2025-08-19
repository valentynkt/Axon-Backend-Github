using System.Text.Json;
using BuildingBlocks.Core.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// In-memory implementation of idempotency cache using IMemoryCache.
/// Suitable for single-instance deployments or development environments.
/// For production distributed scenarios, consider Redis implementation.
/// </summary>
public sealed class InMemoryIdempotencyCache : IIdempotencyCache
{
    private readonly IMemoryCache _memoryCache;

    public InMemoryIdempotencyCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) 
        where T : class
    {
        if (_memoryCache.TryGetValue(key, out var cachedValue))
        {
            if (cachedValue is string json)
            {
                // Deserialize if stored as JSON string
                var deserialized = JsonSerializer.Deserialize<T>(json);
                return Task.FromResult(deserialized);
            }
            
            if (cachedValue is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }
        }

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) 
        where T : class
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            SlidingExpiration = null // No sliding expiration for idempotency
        };

        // Store as JSON string for consistency
        var json = JsonSerializer.Serialize(value);
        _memoryCache.Set(key, json, cacheOptions);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}