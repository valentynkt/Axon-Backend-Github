using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Base record for queries returning Result&lt;TResponse&gt;, with declarative caching.
/// </summary>
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, IQuery<TResponse>
    where TResponse : notnull
{
    /// <summary>Opt-in caching (defaults to false).</summary>
    public virtual bool UseCache { get; init; }

    /// <summary>Optional cache duration.</summary>
    public virtual TimeSpan? CacheDuration { get; init; }

    /// <summary>Override to customize key prefix (defaults to type name).</summary>
    public virtual string CacheKeyPrefix => GetType().Name;

    /// <summary>Returns a copy with caching enabled and optional duration.</summary>
    public T AsCached<T>(TimeSpan? duration = null) where T : QueryBase<TResponse>
        => (T)this with { UseCache = true, CacheDuration = duration };
}