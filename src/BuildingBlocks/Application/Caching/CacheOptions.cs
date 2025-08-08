namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Configuration options for Epic 04 Story 02 declarative query caching.
/// Controls cache behavior, durations, providers, and W3C context integration.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Default cache duration when not specified by query.
    /// Used when IQuery.CacheDuration is null.
    /// </summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Whether to include W3C TraceContext in cache keys for request isolation.
    /// When true, each trace gets its own cache entries.
    /// When false, cache is shared globally (better hit rates).
    /// </summary>
    public bool IncludeTraceInKey { get; set; } = false;
    
    /// <summary>
    /// Compression threshold for distributed cache entries (in bytes).
    /// Entries larger than this will be compressed before storage.
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024; // 1KB
    
    /// <summary>
    /// Redis connection string for distributed caching.
    /// Falls back to memory-only caching if Redis is unavailable.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";
    
    /// <summary>
    /// Redis instance name for key prefixing and isolation.
    /// Allows multiple applications to share the same Redis instance.
    /// </summary>
    public string InstanceName { get; set; } = "axon";
    
    /// <summary>
    /// Redis database number to use (0-15 typically).
    /// Provides logical separation within a Redis instance.
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;
    
    /// <summary>
    /// Memory cache size limit in megabytes.
    /// Controls L1 cache memory usage to prevent unbounded growth.
    /// </summary>
    public int MemoryCacheSizeLimitMB { get; set; } = 100;
    
    /// <summary>
    /// Maximum duration allowed for L1 (memory) cache entries.
    /// Prevents long-lived entries from consuming memory indefinitely.
    /// </summary>
    public TimeSpan MaxMemoryCacheDuration { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Whether caching is enabled globally.
    /// When false, all cache operations are bypassed.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Timeout for cache operations before falling back to handler execution.
    /// Prevents cache slowness from affecting query performance.
    /// </summary>
    public TimeSpan CacheOperationTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
}