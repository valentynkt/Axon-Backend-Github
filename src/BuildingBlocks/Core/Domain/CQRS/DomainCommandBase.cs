using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using BuildingBlocks.Core.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.Caching;

namespace BuildingBlocks.Core.Domain.CQRS;

/// <summary>
/// Epic 2 base class for domain commands with comprehensive validation integration.
/// Provides domain rule validation that integrates with Epic 5 ValidationBehavior.
/// </summary>
public abstract record DomainCommandBase : CommandBase
{
    /// <summary>
    /// Gets the primary aggregate type this command operates on.
    /// Used for Epic 5 pipeline behaviors (transaction scoping, caching, etc.).
    /// </summary>
    public abstract Type GetAggregateType();

    /// <summary>
    /// Domain-specific business rules validation.
    /// Executed by Epic 5 ValidationBehavior after FluentValidation structural validation.
    /// </summary>
    public abstract Validation<Unit> ValidateDomainRules();
}

/// <summary>
/// Epic 2 base class for domain commands that return responses.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract record DomainCommandBase<TResponse> : CommandBase<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Gets the primary aggregate type this command operates on.
    /// </summary>
    public abstract Type GetAggregateType();

    /// <summary>
    /// Domain-specific business rules validation.
    /// </summary>
    public abstract Validation<Unit> ValidateDomainRules();
}

/// <summary>
/// Epic 2 base class for cacheable domain queries with Epic 5 integration.
/// Provides cache key generation and invalidation support.
/// </summary>
public abstract record CacheableDomainQueryBase<TResponse> : QueryBase<TResponse>, ICacheRequest<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Generate cache key for this query.
    /// Should be deterministic and include all relevant parameters.
    /// </summary>
    public abstract string GetCacheKey();

    /// <summary>
    /// Cache duration for this query result.
    /// Return null to use default cache duration.
    /// </summary>
    public abstract TimeSpan? GetCacheDuration();

    /// <summary>
    /// Cache tags for invalidation support.
    /// Used by Epic 5 caching behavior for targeted cache invalidation.
    /// </summary>
    public abstract IEnumerable<string> GetCacheTags();

    /// <summary>
    /// ICacheRequest implementation - returns the cache key.
    /// </summary>
    string ICacheRequest<TResponse>.CacheKey => GetCacheKey();

    /// <summary>
    /// ICacheRequest implementation - returns the cache duration.
    /// </summary>
    TimeSpan? ICacheRequest<TResponse>.Duration => GetCacheDuration();
}

/// <summary>
/// Epic 2 base class for domain queries that can be cached but with simple cache key.
/// </summary>
public abstract record SimpleCacheableDomainQueryBase<TResponse> : CacheableDomainQueryBase<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Simple cache key based on query type and hash of properties.
    /// Override GetCacheKey() for more sophisticated cache key generation.
    /// </summary>
    public override string GetCacheKey()
    {
        var queryType = GetType().Name;
        var propertiesHash = GetPropertiesHash();
        return $"{queryType}:{propertiesHash}";
    }

    /// <summary>
    /// Default cache duration of 5 minutes.
    /// Override for different cache durations.
    /// </summary>
    public override TimeSpan? GetCacheDuration() => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default cache tags based on query type.
    /// Override for more specific cache invalidation.
    /// </summary>
    public override IEnumerable<string> GetCacheTags() 
    {
        yield return GetType().Name;
    }

    /// <summary>
    /// Generates hash code from all public properties.
    /// Used for automatic cache key generation.
    /// </summary>
    protected virtual string GetPropertiesHash()
    {
        var properties = GetType().GetProperties()
            .Where(p => p.CanRead && p.GetMethod?.IsPublic == true)
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{p.GetValue(this)}")
            .ToArray();

        var combined = string.Join("|", properties);
        return combined.GetHashCode().ToString("X8");
    }
}

/// <summary>
/// Marker interface for retryable operations.
/// Used by Epic 5 RetryBehavior to identify commands/queries that should be retried.
/// </summary>
public interface IRetryableOperation
{
    /// <summary>
    /// Gets the retry policy name.
    /// Return null to use default retry policy.
    /// </summary>
    string? GetRetryPolicyName() => null;
}

/// <summary>
/// Marker interface for commands that should invalidate cache.
/// Used by Epic 5 cache invalidation behavior.
/// </summary>
public interface ICacheInvalidatingCommand : IInvalidateCacheRequest
{
    /// <summary>
    /// Gets cache tags to invalidate after successful command execution.
    /// </summary>
    new IEnumerable<string> GetCacheTagsToInvalidate();

    /// <summary>
    /// IInvalidateCacheRequest implementation.
    /// </summary>
    IEnumerable<string> IInvalidateCacheRequest.CacheTagsToInvalidate => GetCacheTagsToInvalidate();
}

/// <summary>
/// Extension methods for domain CQRS patterns.
/// </summary>
public static class DomainCqrsExtensions
{
    /// <summary>
    /// Converts validation result to Result for command handlers.
    /// </summary>
    public static Result<Unit> ToResult(this Validation<Unit> validation)
    {
        return validation.IsValid
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.Failure(Error.Aggregate(validation.Errors.ToArray()));
    }

    /// <summary>
    /// Converts validation result to Result with specific value.
    /// </summary>
    public static Result<T> ToResult<T>(this Validation<T> validation) where T : notnull
    {
        return validation.IsValid
            ? Result<T>.Success(validation.Value)
            : Result<T>.Failure(Error.Aggregate(validation.Errors.ToArray()));
    }
}