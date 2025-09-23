using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Simple two-level cache (memory + distributed) you call from handlers.
/// No pipeline, tags, or extras. TTL-based only.
/// </summary>
public static class HybridCache
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Try L1 (IMemoryCache), then L2 (IDistributedCache), else build via factory.
    /// Stores in both with the same TTL.
    /// </summary>
    public static async Task<T> GetOrCreateAsync<T>(
        IMemoryCache memory,
        IDistributedCache distributed,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(distributed);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (memory.TryGetValue(key, out T? inMem) && inMem is not null)
            return inMem;

        var bytes = await distributed.GetAsync(key, ct);
        if (bytes is { Length: > 0 })
        {
            var fromDist = JsonSerializer.Deserialize<T>(bytes, Json);
            if (fromDist is not null)
            {
                memory.Set(key, fromDist, ttl);
                return fromDist;
            }
        }

        var value = await factory(ct);

        var payload = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        await distributed.SetAsync(
            key,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            ct);

        memory.Set(key, value, ttl);

        return value;
    }

    /// <summary>Best-effort removal from both layers.</summary>
    public static async Task InvalidateAsync(
        IMemoryCache memory,
        IDistributedCache distributed,
        string key,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(distributed);

        memory.Remove(key);
        await distributed.RemoveAsync(key, ct);
    }
}
