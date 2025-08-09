using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Interface for queries that return data without modifying system state wrapped in Result pattern.
/// Queries represent read operations following CQS principle with declarative caching support.
/// All query results are wrapped in Result&lt;T&gt; for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of data this query returns</typeparam>
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    /// <summary>
    /// Indicates whether this query result should be cached.
    /// Enables declarative caching at the query level.
    /// </summary>
    bool UseCache { get; }
    
    /// <summary>
    /// Duration for which the query result should be cached.
    /// If null and UseCache is true, default cache duration will be applied.
    /// </summary>
    TimeSpan? CacheDuration { get; }
    
    /// <summary>
    /// Cache key prefix for this query type.
    /// Defaults to the query type name for automatic cache key generation.
    /// </summary>
    string CacheKeyPrefix => GetType().Name;
}