using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance caching system for Error objects with string interning,
/// memory optimization, and intelligent cache eviction policies.
/// Provides zero-allocation paths for frequently used error patterns.
/// 
/// Note: No background timers. Call CleanupNow() from your host/scheduler when desired.
/// </summary>
public sealed class ErrorCache : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ErrorCache> _logger;
    private readonly ErrorCacheOptions _options;
    private readonly object _lockObject = new();
    private bool _disposed;

    // String interning for error codes, messages, sources, correlation IDs
    private readonly ConcurrentDictionary<string, string> _internedCodes;
    private readonly ConcurrentDictionary<string, string> _internedMessages;
    private readonly ConcurrentDictionary<string, string> _internedSources;
    private readonly ConcurrentDictionary<string, string> _internedCorrelationIds;

    // Cached error instances by composite key
    private readonly ConcurrentDictionary<ErrorCacheKey, Error> _errorCache;

    // Metadata for cache analytics
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private long _internCount;
    private readonly object _metricsLock = new();

    private DateTimeOffset _lastCleanupTime = DateTimeOffset.MinValue;

    public ErrorCache(
        IMemoryCache memoryCache,
        IOptions<ErrorCacheOptions> options,
        ILogger<ErrorCache> logger)
    {
        _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new ErrorCacheOptions();

        _internedCodes = new ConcurrentDictionary<string, string>();
        _internedMessages = new ConcurrentDictionary<string, string>();
        _internedSources = new ConcurrentDictionary<string, string>();
        _internedCorrelationIds = new ConcurrentDictionary<string, string>();
        _errorCache = new ConcurrentDictionary<ErrorCacheKey, Error>();
    }

    #region Error Caching

    /// <summary>
    /// Get cached Error or create and cache new one if not found
    /// </summary>
    public Error GetOrCreateError(
        string code,
        string message,
        ErrorType type = ErrorType.Internal,
        ErrorSeverity? severity = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? correlationId = null,
        string? source = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorCache));

        // Create cache key
        var effSeverity = severity ?? GetDefaultSeverity(type);
        var cacheKey = new ErrorCacheKey(
            code: InternString(code, _internedCodes),
            message: InternString(message, _internedMessages),
            type: type,
            severity: effSeverity,
            hasInnerException: innerException != null,
            hasMetadata: metadata != null && metadata.Count > 0,
            correlationId: correlationId != null ? InternString(correlationId, _internedCorrelationIds) : null,
            source: source != null ? InternString(source, _internedSources) : null
        );

        // Check cache first (fast path for common errors)
        if (_errorCache.TryGetValue(cacheKey, out var cachedError))
        {
            RecordCacheHit();

            // For errors with dynamic content (inner exceptions, metadata), create new instance
            if (cacheKey.HasInnerException || cacheKey.HasMetadata)
            {
                return CreateErrorWithDynamicContent(cachedError, innerException, metadata, effSeverity);
            }

            return cachedError;
        }

        // Cache miss - create new error
        RecordCacheMiss();
        var newError = CreateError(
            cacheKey.Code,
            cacheKey.Message,
            cacheKey.Type,
            effSeverity,
            innerException,
            metadata,
            cacheKey.CorrelationId,
            cacheKey.Source
        );

        // Cache the error if it meets caching criteria
        if (ShouldCacheError(cacheKey, newError))
        {
            CacheError(cacheKey, newError);
        }

        return newError;
    }

    /// <summary>
    /// Get cached Error by cache key (for performance-critical paths)
    /// </summary>
    public Error? TryGetCachedError(ErrorCacheKey cacheKey)
    {
        if (_disposed) return null;

        if (_errorCache.TryGetValue(cacheKey, out var cachedError))
        {
            RecordCacheHit();
            return cachedError;
        }

        RecordCacheMiss();
        return null;
    }

    /// <summary>
    /// Pre-cache common error patterns for zero-allocation access
    /// </summary>
    public void PreCacheCommonErrors()
    {
        if (_disposed) return;

        var commonErrors = new[]
        {
            // Validation errors
            ("VALIDATION_REQUIRED", "Value is required", ErrorType.Validation),
            ("VALIDATION_INVALID_FORMAT", "Invalid format", ErrorType.Validation),
            ("VALIDATION_OUT_OF_RANGE", "Value is out of range", ErrorType.Validation),

            // Not found errors
            ("ENTITY_NOT_FOUND", "Entity not found", ErrorType.NotFound),
            ("RESOURCE_NOT_FOUND", "Resource not found", ErrorType.NotFound),

            // Business rule errors
            ("BUSINESS_RULE_VIOLATED", "Business rule violated", ErrorType.BusinessRule),
            ("INSUFFICIENT_PERMISSIONS", "Insufficient permissions", ErrorType.BusinessRule),

            // System errors
            ("INTERNAL_SERVER_ERROR", "Internal server error", ErrorType.Internal),
            ("SERVICE_UNAVAILABLE", "Service temporarily unavailable", ErrorType.External),
            ("REQUEST_TIMEOUT", "Request timeout", ErrorType.Timeout),

            // Conflict errors
            ("DUPLICATE_ENTITY", "Entity already exists", ErrorType.Conflict),
            ("CONCURRENT_MODIFICATION", "Concurrent modification detected", ErrorType.Conflict)
        };

        foreach (var (code, message, type) in commonErrors)
        {
            var cacheKey = new ErrorCacheKey(
                code: InternString(code, _internedCodes),
                message: InternString(message, _internedMessages),
                type: type,
                severity: GetDefaultSeverity(type),
                hasInnerException: false,
                hasMetadata: false,
                correlationId: null,
                source: null
            );

            var error = CreateError(code, message, type, GetDefaultSeverity(type));
            CacheError(cacheKey, error);
        }

        _logger.LogInformation("Pre-cached {Count} common error patterns", commonErrors.Length);
    }

    #endregion

    #region String Interning

    /// <summary>
    /// Intern string for memory optimization (zero-allocation for repeated strings)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternString(string value, ConcurrentDictionary<string, string> cache)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length > _options.MaxInternedStringLength)
            return value; // Don't intern very long strings

        return cache.GetOrAdd(value, v =>
        {
            RecordInternOperation();
            return string.Intern(v);
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternErrorCode(string code) => InternString(code, _internedCodes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternErrorMessage(string message) => InternString(message, _internedMessages);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternErrorSource(string source) => InternString(source, _internedSources);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternCorrelationId(string correlationId) => InternString(correlationId, _internedCorrelationIds);

    #endregion

    #region Cache Management

    /// <summary>
    /// Manual cleanup based on memory pressure. No timer — call from a scheduler.
    /// Returns bytes freed (best-effort estimate).
    /// </summary>
    public long CleanupNow()
    {
        if (_disposed) return 0;

        try
        {
            var before = GC.GetTotalMemory(false);

            if (before > _options.MemoryThresholdBytes)
            {
                Compact(_options.CompactionPercentage);
                GC.Collect(0, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                GC.Collect(1, GCCollectionMode.Optimized);
            }

            var after = GC.GetTotalMemory(false);
            var freed = Math.Max(0, before - after);

            _lastCleanupTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("ErrorCache cleanup completed, freed {FreedBytes} bytes", freed);
            return freed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
            return 0;
        }
    }

    /// <summary>
    /// Clear all cached errors and interned strings
    /// </summary>
    public void Clear()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            _errorCache.Clear();
            _internedCodes.Clear();
            _internedMessages.Clear();
            _internedSources.Clear();
            _internedCorrelationIds.Clear();
            _cache.Compact(1.0); // Compact underlying memory cache

            ResetMetrics();
            _lastCleanupTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("Error cache cleared");
        }
    }

    /// <summary>
    /// Get cache statistics for monitoring and diagnostics
    /// </summary>
    public ErrorCacheStatistics GetStatistics()
    {
        if (_disposed) return new ErrorCacheStatistics();

        lock (_metricsLock)
        {
            var totalRequests = _hitCount + _missCount;
            var hitRatio = totalRequests > 0 ? (double)_hitCount / totalRequests : 0.0;

            return new ErrorCacheStatistics
            {
                HitCount = _hitCount,
                MissCount = _missCount,
                EvictionCount = _evictionCount,
                InternCount = _internCount,
                HitRatio = hitRatio,
                CachedErrorCount = _errorCache.Count,
                InternedCodeCount = _internedCodes.Count,
                InternedMessageCount = _internedMessages.Count,
                InternedSourceCount = _internedSources.Count,
                InternedCorrelationIdCount = _internedCorrelationIds.Count,
                MemoryPressure = GC.GetTotalMemory(false),
                LastCleanupTime = _lastCleanupTime
            };
        }
    }

    /// <summary>
    /// Compact cache by removing least recently used items (best-effort).
    /// </summary>
    public void Compact(double percentage = 0.2)
    {
        if (_disposed) return;

        if (percentage <= 0 || percentage >= 1.0)
            return;

        lock (_lockObject)
        {
            var targetRemovalCount = (int)(_errorCache.Count * percentage);
            if (targetRemovalCount == 0) return;

            // Simple approach - remove random items (consider LRU if needed)
            var itemsToRemove = _errorCache.Keys.Take(targetRemovalCount).ToList();

            foreach (var key in itemsToRemove)
            {
                if (_errorCache.TryRemove(key, out _))
                {
                    RecordEviction();
                }
            }

            _logger.LogInformation("Compacted error cache, removed {Count} items", itemsToRemove.Count);
        }
    }

    #endregion

    #region Private Methods

    private Error CreateError(
        string code,
        string message,
        ErrorType type,
        ErrorSeverity severity,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? correlationId = null,
        string? source = null)
    {
        // Use Error factory methods to maintain consistency
        var created = type switch
        {
            ErrorType.Validation        => Error.Validation(message, code, metadata),
            ErrorType.NotFound          => Error.NotFound(message, code, metadata),
            ErrorType.BusinessRule      => Error.BusinessRule(message, code, metadata),
            ErrorType.Conflict          => Error.Conflict(message, code, metadata),
            ErrorType.Unauthorized      => Error.Unauthorized(message, code, metadata),
            ErrorType.Forbidden         => Error.Forbidden(message, code, metadata),
            ErrorType.PreconditionFailed=> Error.PreconditionFailed(message, code, metadata),
            ErrorType.RateLimit         => Error.RateLimit(message, code, metadata),
            ErrorType.Cancelled         => Error.Cancelled(message, code, metadata),
            ErrorType.Serialization     => Error.Serialization(message, code, metadata),
            ErrorType.Concurrency       => Error.Concurrency(message, code, metadata),
            ErrorType.Internal          => Error.Internal(message, code, innerException, metadata),
            ErrorType.Configuration     => Error.Configuration(message, code, null, metadata),
            ErrorType.External          => Error.External(message, code, innerException, metadata),
            ErrorType.Network           => Error.Network(message, code, innerException, metadata),
            ErrorType.Timeout           => Error.Timeout(message, code, timeout: null, metadata: metadata),
            ErrorType.Unavailable       => Error.Unavailable(message, code, metadata),
            ErrorType.Persistence       => Error.Persistence(message, code, innerException, metadata),
            ErrorType.Aggregate         => Error.Aggregate(message, code, metadata),
            ErrorType.Security          => Error.Security(message, code, metadata),
            _                           => Error.Internal(message, code, innerException, metadata)
        };

        // Apply non-default severity if requested (while keeping same instance semantics)
        var defaultSeverity = GetDefaultSeverity(type);
        if (severity != defaultSeverity)
        {
            return created.WithSeverity(severity);
        }

        return created;
    }

    private Error CreateErrorWithDynamicContent(
        Error template,
        Exception? innerException,
        IReadOnlyDictionary<string, object>? metadata,
        ErrorSeverity severity)
    {
        var created = CreateError(
            template.Code,
            template.Message,
            template.Type,
            severity,
            innerException,
            metadata,
            template.CorrelationId,
            template.Source
        );

        // preserve correlation/source if template had them but switch didn’t set
        if (template.CorrelationId is { } cid && created.CorrelationId is null)
            created = created.WithCorrelationId(cid);
        if (template.Source is { } s && created.Source is null)
            created = created.WithSource(s);

        return created;
    }

    private bool ShouldCacheError(ErrorCacheKey key, Error error)
    {
        // Don't cache errors with dynamic content
        if (key.HasInnerException || key.HasMetadata)
            return false;

        // Don't cache if we're at capacity
        if (_errorCache.Count >= _options.MaxCachedErrors)
            return false;

        // Don't cache very long messages
        if (key.Message.Length > _options.MaxCachedMessageLength)
            return false;

        return true;
    }

    private void CacheError(ErrorCacheKey key, Error error)
    {
        _errorCache.TryAdd(key, error);

        // Also cache in IMemoryCache with expiration for automatic cleanup
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            Size = EstimateErrorSize(error),
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes),
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = OnErrorEvicted
                }
            }
        };

        _cache.Set($"error:{key.GetHashCode()}", error, cacheEntryOptions);
    }

    private void OnErrorEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        RecordEviction();
        _logger.LogDebug("Error cache entry evicted: {Reason}", reason);
    }

    private static long EstimateErrorSize(Error error)
    {
        // Simple size estimation for memory cache sizing
        var size = error.Code.Length + error.Message.Length + 100; // Base overhead

        if (error.Metadata != null)
        {
            size += error.Metadata.Count * 50; // Estimated metadata overhead
        }

        return size;
    }

    private static ErrorSeverity GetDefaultSeverity(ErrorType type)
    {
        return type switch
        {
            ErrorType.Validation        => ErrorSeverity.Warning,
            ErrorType.NotFound          => ErrorSeverity.Info,
            ErrorType.Conflict          => ErrorSeverity.Warning,
            ErrorType.BusinessRule      => ErrorSeverity.Warning,
            ErrorType.Unauthorized      => ErrorSeverity.Warning,
            ErrorType.Forbidden         => ErrorSeverity.Warning,
            ErrorType.PreconditionFailed=> ErrorSeverity.Warning,
            ErrorType.RateLimit         => ErrorSeverity.Warning,
            ErrorType.Cancelled         => ErrorSeverity.Info,
            ErrorType.Serialization     => ErrorSeverity.Error,
            ErrorType.Concurrency       => ErrorSeverity.Warning,
            ErrorType.Internal          => ErrorSeverity.Critical,
            ErrorType.Configuration     => ErrorSeverity.Critical,
            ErrorType.External          => ErrorSeverity.Error,
            ErrorType.Network           => ErrorSeverity.Error,
            ErrorType.Timeout           => ErrorSeverity.Warning,
            ErrorType.Unavailable       => ErrorSeverity.Error,
            ErrorType.Persistence       => ErrorSeverity.Critical,
            ErrorType.Aggregate         => ErrorSeverity.Error,
            ErrorType.Security          => ErrorSeverity.Fatal,
            _                           => ErrorSeverity.Error
        };
    }

    #endregion

    #region Metrics

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordCacheHit() => Interlocked.Increment(ref _hitCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordCacheMiss() => Interlocked.Increment(ref _missCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordEviction() => Interlocked.Increment(ref _evictionCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordInternOperation() => Interlocked.Increment(ref _internCount);

    private void ResetMetrics()
    {
        lock (_metricsLock)
        {
            _hitCount = 0;
            _missCount = 0;
            _evictionCount = 0;
            _internCount = 0;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ErrorCache disposal");
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Configuration options for ErrorCache
/// </summary>
public sealed class ErrorCacheOptions
{
    /// <summary>
    /// Maximum number of cached error instances (default: 1000)
    /// </summary>
    public int MaxCachedErrors { get; set; } = 1000;

    /// <summary>
    /// Maximum length of error messages to cache (default: 500)
    /// </summary>
    public int MaxCachedMessageLength { get; set; } = 500;

    /// <summary>
    /// Maximum length of strings to intern (default: 200)
    /// </summary>
    public int MaxInternedStringLength { get; set; } = 200;

    /// <summary>
    /// Sliding expiration time for cached errors in minutes (default: 30)
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Absolute expiration time for cached errors in minutes (default: 60)
    /// </summary>
    public int AbsoluteExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Cleanup interval (legacy). Not used after timer removal; kept for compat.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Memory threshold in bytes for triggering cleanup (default: 50MB)
    /// </summary>
    public long MemoryThresholdBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Percentage of cache to remove during compaction (default: 0.2 = 20%)
    /// </summary>
    public double CompactionPercentage { get; set; } = 0.2;
}

/// <summary>
/// Cache key for Error instances with value semantics
/// </summary>
public readonly record struct ErrorCacheKey(
    string Code,
    string Message,
    ErrorType Type,
    ErrorSeverity Severity,
    bool HasInnerException,
    bool HasMetadata,
    string? CorrelationId,
    string? Source);

/// <summary>
/// Statistics for ErrorCache monitoring and diagnostics
/// </summary>
public sealed record ErrorCacheStatistics
{
    public long HitCount { get; init; }
    public long MissCount { get; init; }
    public long EvictionCount { get; init; }
    public long InternCount { get; init; }
    public double HitRatio { get; init; }
    public int CachedErrorCount { get; init; }
    public int InternedCodeCount { get; init; }
    public int InternedMessageCount { get; init; }
    public int InternedSourceCount { get; init; }
    public int InternedCorrelationIdCount { get; init; }
    public long MemoryPressure { get; init; }
    public DateTimeOffset LastCleanupTime { get; init; }
}
