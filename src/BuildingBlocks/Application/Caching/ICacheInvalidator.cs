using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Invalidation that works across nodes: uses a tag index to discover keys,
/// deletes keys from distributed cache, and evicts from local memory cache.
/// </summary>
public interface ICacheInvalidator
{
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}

public sealed class CacheInvalidator : ICacheInvalidator
{
    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _distributed;
    private readonly ICacheTagIndex _tagIndex;
    private readonly ILogger<CacheInvalidator> _logger;

    public CacheInvalidator(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ICacheTagIndex tagIndex,
        ILogger<CacheInvalidator> logger)
    {
        _memory = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributed = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _tagIndex = tagIndex ?? throw new ArgumentNullException(nameof(tagIndex));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var tagArray = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToArray();
        if (tagArray.Length == 0) return;

        var keys = await _tagIndex.GetKeysAsync(tagArray, cancellationToken);
        if (keys.Length == 0)
        {
            _logger.LogDebug("No keys found for tags: {Tags}", string.Join(",", tagArray));
            return;
        }

        _logger.LogDebug("Invalidating {Count} keys for tags: {Tags}", keys.Length, string.Join(",", tagArray));

        foreach (var key in keys)
        {
            try
            {
                // Best-effort local L1 eviction (IMemoryCache has no Remove(key) that guarantees cross-node)
                _memory.Remove(key);
                await _distributed.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cache key {Key}", key);
            }
        }

        // Finally, remove the tag indexes (fresh rebuild on next cache write)
        await _tagIndex.RemoveAsync(tagArray, cancellationToken);
    }
}