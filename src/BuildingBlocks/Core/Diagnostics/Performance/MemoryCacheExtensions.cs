using Microsoft.Extensions.Caching.Memory;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// Extension methods for IMemoryCache to provide additional functionality
/// not available in the interface but present in the concrete implementation.
/// </summary>
public static class MemoryCacheExtensions
{
    /// <summary>
    /// Attempts to compact the memory cache by the specified percentage if the underlying
    /// implementation supports it. This method provides a safe way to access the Compact
    /// method on MemoryCache through the IMemoryCache interface.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance</param>
    /// <param name="compactionPercentage">Percentage of cache entries to remove (0.0 to 1.0)</param>
    /// <returns>True if compaction was performed, false if not supported</returns>
    public static bool TryCompact(this IMemoryCache memoryCache, double compactionPercentage)
    {
        if (memoryCache is MemoryCache concreteCache)
        {
            concreteCache.Compact(compactionPercentage);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Compacts the memory cache by the specified percentage, throwing an exception
    /// if the underlying implementation doesn't support compaction.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance</param>
    /// <param name="compactionPercentage">Percentage of cache entries to remove (0.0 to 1.0)</param>
    /// <exception cref="NotSupportedException">Thrown when the underlying implementation doesn't support compaction</exception>
    public static void Compact(this IMemoryCache memoryCache, double compactionPercentage)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);

        if (!TryCompact(memoryCache, compactionPercentage))
        {
            throw new NotSupportedException(
                $"The underlying memory cache implementation ({memoryCache.GetType().Name}) does not support compaction. " +
                "Consider using Microsoft.Extensions.Caching.Memory.MemoryCache for compaction support.");
        }
    }
}