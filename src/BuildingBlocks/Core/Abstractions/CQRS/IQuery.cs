using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Queries return data without side effects, wrapped in Result&lt;TResponse&gt;.
/// Includes declarative cache hints (opt-in).
/// </summary>
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
    /// <summary>Enable caching for this query instance.</summary>
    bool UseCache { get; }

    /// <summary>Optional cache duration; if null and UseCache = true, a default may be applied.</summary>
    TimeSpan? CacheDuration { get; }

    /// <summary>Cache key prefix (defaults to type name).</summary>
    string CacheKeyPrefix => GetType().Name;
}