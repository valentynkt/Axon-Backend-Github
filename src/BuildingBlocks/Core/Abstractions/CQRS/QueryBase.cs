using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Abstract base record for queries that return data wrapped in Result pattern.
/// Provides consistent implementation for query identification, timestamps, and declarative caching.
/// Uses record type for value equality and immutability benefits.
/// Queries return Result pattern for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this query returns</typeparam>
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, IQuery<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Indicates whether this query result should be cached.
    /// Defaults to false for opt-in caching behavior.
    /// </summary>
    public virtual bool UseCache { get; init; }
    
    /// <summary>
    /// Duration for which the query result should be cached.
    /// If null and UseCache is true, default cache duration will be applied.
    /// </summary>
    public virtual TimeSpan? CacheDuration { get; init; }
    
    /// <summary>
    /// Cache key prefix for this query type.
    /// Defaults to the query type name for automatic cache key generation.
    /// </summary>
    public virtual string CacheKeyPrefix => GetType().Name;
    
    /// <summary>
    /// Creates a new instance of the query with caching enabled.
    /// Uses record's copy semantics to maintain immutability.
    /// </summary>
    /// <typeparam name="T">The concrete query type</typeparam>
    /// <param name="duration">Optional cache duration. If null, default duration will be used.</param>
    /// <returns>New instance with caching enabled</returns>
    public T AsCached<T>(TimeSpan? duration = null) where T : QueryBase<TResponse>
    {
        return (T)this with { UseCache = true, CacheDuration = duration };
    }
}