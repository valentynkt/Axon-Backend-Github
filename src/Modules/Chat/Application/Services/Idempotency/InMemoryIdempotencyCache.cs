using Axon.Modules.Chat.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Axon.Modules.Chat.Application.Services.Idempotency;

/// <summary>
/// In-memory implementation of idempotency cache
/// Used for preventing duplicate processing of requests within a short time window
/// </summary>
public sealed class InMemoryIdempotencyCache : IIdempotencyCache
{
    private readonly IMemoryCache _memoryCache;

    public InMemoryIdempotencyCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
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

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
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