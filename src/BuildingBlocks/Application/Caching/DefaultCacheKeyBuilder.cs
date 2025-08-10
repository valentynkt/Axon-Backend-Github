using System.Globalization;
using System.Text;

namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Default implementation of cache key builder.
/// Follows standard Axon cache key format conventions.
/// </summary>
public sealed class DefaultCacheKeyBuilder : ICacheKeyBuilder
{
    private static readonly CompositeFormat QueryKeyComposite = CompositeFormat.Parse("axon:query:{0}:{1}:{2}");
    private static readonly CompositeFormat FallbackKeyComposite = CompositeFormat.Parse("axon:cache:{0}:v1:{1}");

    public string BuildQueryKey(string prefix, string contentHash, string contextPart)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextPart);

        return string.Format(CultureInfo.InvariantCulture, QueryKeyComposite, prefix, contentHash, contextPart);
    }

    public string BuildFallbackKey(string typeName, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        return string.Format(CultureInfo.InvariantCulture, FallbackKeyComposite, typeName, contentHash);
    }
}