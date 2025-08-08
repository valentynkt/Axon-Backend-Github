using System.Reflection;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Abstractions.CQRS;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Pipeline behavior that invalidates caches for commands in Epic 05.
/// Provides tag-based cache invalidation to maintain data consistency.
/// </summary>
public sealed class InvalidateCachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>  // Only for commands
    where TResponse : IResult
{
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly ILogger<InvalidateCachingBehavior<TRequest, TResponse>> _logger;

    public InvalidateCachingBehavior(
        ICacheInvalidator cacheInvalidator,
        ILogger<InvalidateCachingBehavior<TRequest, TResponse>> logger)
    {
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Execute the command first
        var response = await next(cancellationToken);

        // Only invalidate caches if the command was successful
        if (response.IsSuccess)
        {
            await InvalidateCaches(request, cancellationToken);
        }

        return response;
    }

    private async Task InvalidateCaches(TRequest request, CancellationToken cancellationToken)
    {
        var invalidationTags = InvalidateCachingBehavior<TRequest, TResponse>.GetInvalidationTags(request);
        if (invalidationTags.Length == 0)
        {
            _logger.LogDebug("No cache invalidation tags configured for {RequestType}",
                typeof(TRequest).Name);
            return;
        }

        try
        {
            _logger.LogDebug("Invalidating caches for {RequestType} with tags: {Tags}",
                typeof(TRequest).Name, string.Join(", ", invalidationTags));

            await _cacheInvalidator.InvalidateByTagsAsync(invalidationTags, cancellationToken);

            _logger.LogDebug("Successfully invalidated caches for {RequestType}",
                typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate caches for {RequestType}",
                typeof(TRequest).Name);
            // Don't throw - cache invalidation failure shouldn't fail the command
        }
    }

    private static string[] GetInvalidationTags(TRequest request)
    {
        // 1. Check if request implements ICacheInvalidator
        if (request is ICacheInvalidatable invalidatableRequest)
        {
            return invalidatableRequest.GetInvalidationTags();
        }

        // 2. Check for InvalidatesCacheAttribute
        var invalidatesAttribute = typeof(TRequest).GetCustomAttribute<InvalidatesCacheAttribute>();
        if (invalidatesAttribute != null)
        {
            return invalidatesAttribute.Tags;
        }

        // 3. Default behavior based on command type
        return InvalidateCachingBehavior<TRequest, TResponse>.GetDefaultInvalidationTags();
    }

    private static string[] GetDefaultInvalidationTags()
    {
        var commandType = typeof(TRequest);
        var commandName = commandType.Name;

        // Simple convention-based invalidation
        if (commandName.StartsWith("Create") || commandName.StartsWith("Update") || commandName.StartsWith("Delete"))
        {
            // Extract entity name from command name
            var entityName = commandName.Replace("Command", "")
                .Replace("Create", "")
                .Replace("Update", "")
                .Replace("Delete", "")
                .ToLowerInvariant();

            if (!string.IsNullOrEmpty(entityName))
            {
                return new[] { entityName, $"{entityName}s" };
            }
        }

        return Array.Empty<string>();
    }
}

/// <summary>
/// Interface for commands that want to specify cache invalidation tags.
/// </summary>
public interface ICacheInvalidatable
{
    string[] GetInvalidationTags();
}

/// <summary>
/// Attribute for declarative cache invalidation configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InvalidatesCacheAttribute : Attribute
{
    public string[] Tags { get; }

    public InvalidatesCacheAttribute(params string[] tags)
    {
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }
}

/// <summary>
/// Service for invalidating caches by tags.
/// </summary>
public interface ICacheInvalidator
{
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}

public sealed class CacheInvalidator : ICacheInvalidator
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheInvalidator> _logger;

    public CacheInvalidator(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<CacheInvalidator> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var tagArray = tags.ToArray();
        if (tagArray.Length == 0)
            return;

        _logger.LogDebug("Invalidating caches for tags: {Tags}", string.Join(", ", tagArray));

        // For now, implement a simple invalidation strategy
        // In production, you might want to use Redis SET operations to track cache keys by tags
        
        // This is a simplified implementation - in production you would:
        // 1. Store tag -> cache key mappings in Redis
        // 2. Query all keys for the given tags
        // 3. Remove all associated cache entries

        // For demonstration, we'll invalidate some common patterns
        foreach (var tag in tagArray)
        {
            await InvalidateByPattern($"*{tag}*");
        }
    }

    private async Task InvalidateByPattern(string pattern)
    {
        // This is a simplified implementation
        // In production with Redis, you would use SCAN with pattern matching
        
        _logger.LogDebug("Invalidating cache entries matching pattern: {Pattern}", pattern);
        
        // For memory cache, we can't easily enumerate keys, so this is a limitation
        // For distributed cache, we would need Redis-specific implementation
        
        await Task.CompletedTask; // Placeholder for actual implementation
    }
}