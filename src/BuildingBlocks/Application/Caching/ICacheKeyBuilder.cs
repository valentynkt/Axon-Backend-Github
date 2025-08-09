namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Builds cache keys from components following a consistent format.
/// Separates key construction logic from hash generation.
/// </summary>
public interface ICacheKeyBuilder
{
    /// <summary>
    /// Builds a cache key for queries.
    /// </summary>
    /// <param name="prefix">The cache key prefix</param>
    /// <param name="contentHash">The content hash</param>
    /// <param name="contextPart">The context part (e.g., trace context)</param>
    /// <returns>A formatted cache key</returns>
    string BuildQueryKey(string prefix, string contentHash, string contextPart);

    /// <summary>
    /// Builds a fallback cache key for non-query requests.
    /// </summary>
    /// <param name="typeName">The type name</param>
    /// <param name="contentHash">The content hash</param>
    /// <returns>A formatted cache key</returns>
    string BuildFallbackKey(string typeName, string contentHash);
}