using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Tag index that maps tags to cache keys in the distributed store,
/// enabling reliable invalidation across nodes.
/// </summary>
public interface ICacheTagIndex
{
    Task IndexAsync(IEnumerable<string> tags, string cacheKey, TimeSpan ttl, CancellationToken ct);
    Task<string[]> GetKeysAsync(IEnumerable<string> tags, CancellationToken ct);
    Task RemoveAsync(IEnumerable<string> tags, CancellationToken ct);
}

/// <summary>
/// Basic JSON-blob implementation over IDistributedCache.
/// NOT for huge tag sets, but fine for MVP.
/// </summary>
public sealed class DistributedCacheTagIndex : ICacheTagIndex
{
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _json;

    public DistributedCacheTagIndex(IDistributedCache cache, IOptions<JsonSerializerOptions>? jsonOptions = null)
    {
        _cache = cache;
        _json = jsonOptions?.Value ?? new JsonSerializerOptions();
    }

    public async Task IndexAsync(IEnumerable<string> tags, string cacheKey, TimeSpan ttl, CancellationToken ct)
    {
        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var key = IndexKey(tag);
            var set = await ReadSet(key, ct);
            if (set.Add(cacheKey))
            {
                await WriteSet(key, set, ttl, ct);
            }
        }
    }

    public async Task<string[]> GetKeysAsync(IEnumerable<string> tags, CancellationToken ct)
    {
        var union = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var set = await ReadSet(IndexKey(tag), ct);
            union.UnionWith(set);
        }
        return union.ToArray();
    }

    public async Task RemoveAsync(IEnumerable<string> tags, CancellationToken ct)
    {
        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            await _cache.RemoveAsync(IndexKey(tag), ct);
    }

    private static string IndexKey(string tag) => $"cache:tag:{tag}";

    private async Task<HashSet<string>> ReadSet(string key, CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0) return new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(bytes, _json) ?? Array.Empty<string>();
            return new HashSet<string>(arr, StringComparer.Ordinal);
        }
        catch
        {
            await _cache.RemoveAsync(key, ct);
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private Task WriteSet(string key, HashSet<string> set, TimeSpan ttl, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(set.ToArray(), _json);
        return _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        }, ct);
    }
}