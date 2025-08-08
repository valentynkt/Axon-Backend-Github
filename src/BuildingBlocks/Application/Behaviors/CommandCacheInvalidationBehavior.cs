using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Invalidates caches after successful commands.
/// Uses tag sets produced by query caching to delete keys from L2 and evict from L1.
/// </summary>
public sealed class CommandCacheInvalidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : IResult
{
    private readonly ICacheInvalidator _invalidator;
    private readonly ILogger<CommandCacheInvalidationBehavior<TRequest, TResponse>> _logger;

    public CommandCacheInvalidationBehavior(
        ICacheInvalidator cacheInvalidator,
        ILogger<CommandCacheInvalidationBehavior<TRequest, TResponse>> logger)
    {
        _invalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(); // MediatR delegate has no token param

        if (response.IsSuccess)
        {
            try
            {
                var tags = CacheInvalidationTags.Resolve(request);
                if (tags.Length > 0)
                {
                    _logger.LogDebug("Invalidating cache for {Request} with tags: {Tags}", typeof(TRequest).Name, string.Join(",", tags));
                    await _invalidator.InvalidateByTagsAsync(tags, cancellationToken);
                }
                else
                {
                    _logger.LogDebug("No cache invalidation tags for {Request}", typeof(TRequest).Name);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // let caller cancel; don't fail command
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache invalidation failed for {Request}", typeof(TRequest).Name);
            }
        }

        return response;
    }

    private static class CacheInvalidationTags
    {
        public static string[] Resolve(TRequest request)
        {
            if (request is ICacheInvalidatable custom && custom.GetInvalidationTags() is { Length: > 0 })
                return custom.GetInvalidationTags();

            var attr = request.GetType().GetCustomAttributes(typeof(InvalidatesCacheAttribute), inherit: true)
                .OfType<InvalidatesCacheAttribute>()
                .FirstOrDefault();

            if (attr is not null && attr.Tags.Length > 0)
                return attr.Tags;

            // convention fallback
            var name = typeof(TRequest).Name;
            if (name.StartsWith("Create") || name.StartsWith("Update") || name.StartsWith("Delete"))
            {
                var entity = name.Replace("Command", string.Empty)
                                 .Replace("Create", string.Empty)
                                 .Replace("Update", string.Empty)
                                 .Replace("Delete", string.Empty)
                                 .ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(entity))
                    return new[] { entity, $"{entity}s" };
            }

            return Array.Empty<string>();
        }
    }
}

/// <summary>
/// Commands can declare invalidation tags programmatically.
/// </summary>
public interface ICacheInvalidatable
{
    string[] GetInvalidationTags();
}

/// <summary>
/// Commands can declare invalidation tags via attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InvalidatesCacheAttribute : Attribute
{
    public string[] Tags { get; }
    public InvalidatesCacheAttribute(params string[] tags) => Tags = tags ?? Array.Empty<string>();
}

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
