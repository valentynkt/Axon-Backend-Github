namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Generates deterministic cache keys for Epic 04 Story 02 declarative query caching.
/// Integrates with IQuery declarative properties and supports W3C TraceContext isolation.
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// Generates a cache key for the given query.
    /// Uses query properties like CacheKeyPrefix and optionally TraceId for key generation.
    /// </summary>
    /// <typeparam name="TQuery">The query type to generate a key for</typeparam>
    /// <param name="query">The query instance containing properties and data</param>
    /// <returns>A deterministic, collision-resistant cache key</returns>
    string GenerateKey<TQuery>(TQuery query) where TQuery : notnull;
}