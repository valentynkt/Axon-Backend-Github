using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance object pool for error metadata dictionaries.
/// Provides zero-allocation paths for frequently used metadata patterns
/// with automatic size management and memory pressure handling.
/// 
/// Note: No background timers. Call PerformMaintenance() manually from your host/scheduler.
/// </summary>
public sealed class ErrorMetadataPool : IDisposable
{
    private readonly ObjectPool<Dictionary<string, object>> _dictionaryPool;
    private readonly ObjectPool<List<KeyValuePair<string, object>>> _keyValueListPool;
    private readonly ErrorMetadataPoolOptions _options;
    private readonly ILogger<ErrorMetadataPool> _logger;

    // Pre-allocated common metadata patterns (read-only snapshots)
    private readonly Dictionary<string, IReadOnlyDictionary<string, object>> _frozenPatterns;

    // Pool statistics (derived where possible)
    private long _totalAllocations;          // total GetDictionary() calls
    private long _totalReturns;              // total ReturnDictionary() calls
    private long _pooledObjectsCreated;      // created by pool policies (dict + list)
    private long _patternPoolHits;           // TryGetCommonPattern() fast path hits
    private long _patternPoolMisses;         // TryGetCommonPattern() misses

    private bool _disposed;
    private readonly object _lockObject = new();

    public ErrorMetadataPool(
        IOptions<ErrorMetadataPoolOptions> options,
        ILogger<ErrorMetadataPool> logger)
    {
        _options = options?.Value ?? new ErrorMetadataPoolOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create object pools with custom policies that count creations
        var provider = new DefaultObjectPoolProvider
        {
            MaximumRetained = _options.MaxPoolSize
        };

        _dictionaryPool = provider.Create(new DictionaryPooledObjectPolicy(_options, OnPooledObjectCreated));
        _keyValueListPool = provider.Create(new KeyValueListPooledObjectPolicy(_options, OnPooledObjectCreated));

        _frozenPatterns = new Dictionary<string, IReadOnlyDictionary<string, object>>();

        InitializeCommonPatterns();
    }

    #region Dictionary Pool Operations

    /// <summary>
    /// Get a pooled dictionary for building metadata (zero-allocation for pooled instances)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object> GetDictionary()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorMetadataPool));

        var dictionary = _dictionaryPool.Get();
        Interlocked.Increment(ref _totalAllocations);
        return dictionary;
    }

    /// <summary>
    /// Return a dictionary to the pool after use
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnDictionary(Dictionary<string, object> dictionary)
    {
        if (_disposed || dictionary == null) return;

        // Clear before returning to pool; policy will decide if it can be kept
        dictionary.Clear();
        _dictionaryPool.Return(dictionary);
        Interlocked.Increment(ref _totalReturns);
    }

    /// <summary>
    /// Get a pooled dictionary and create read-only wrapper in one operation
    /// </summary>
    public IReadOnlyDictionary<string, object> CreateReadOnlyMetadata(
        Action<Dictionary<string, object>> buildAction)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorMetadataPool));

        ArgumentNullException.ThrowIfNull(buildAction);

        var dictionary = GetDictionary();
        try
        {
            buildAction(dictionary);
            // Create immutable copy for safety
            return new Dictionary<string, object>(dictionary);
        }
        finally
        {
            ReturnDictionary(dictionary);
        }
    }

    /// <summary>
    /// Create metadata dictionary using builder pattern with automatic pool management
    /// </summary>
    public IReadOnlyDictionary<string, object> BuildMetadata(params (string Key, object Value)[] entries)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorMetadataPool));

        if (entries.Length == 0)
            return EmptyMetadata;

        var dictionary = GetDictionary();
        try
        {
            foreach (var (key, value) in entries)
            {
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    dictionary[key] = value;
                }
            }

            return new Dictionary<string, object>(dictionary);
        }
        finally
        {
            ReturnDictionary(dictionary);
        }
    }

    #endregion

    #region Common Pattern Management

    /// <summary>
    /// Get pre-allocated common metadata pattern (zero-allocation)
    /// </summary>
    public IReadOnlyDictionary<string, object>? TryGetCommonPattern(string patternKey)
    {
        if (_disposed) return null;

        if (_frozenPatterns.TryGetValue(patternKey, out var frozenPattern))
        {
            Interlocked.Increment(ref _patternPoolHits);
            return frozenPattern;
        }

        Interlocked.Increment(ref _patternPoolMisses);
        return null;
    }

    /// <summary>
    /// Register a common metadata pattern for reuse
    /// </summary>
    public void RegisterCommonPattern(string patternKey, IReadOnlyDictionary<string, object> metadata)
    {
        if (_disposed) return;

        ArgumentNullException.ThrowIfNull(patternKey);
        ArgumentNullException.ThrowIfNull(metadata);

        lock (_lockObject)
        {
            if (_frozenPatterns.Count >= _options.MaxCommonPatterns)
            {
                _logger.LogWarning("Maximum common patterns reached, cannot register pattern: {PatternKey}", patternKey);
                return;
            }

            _frozenPatterns[patternKey] = metadata;
            _logger.LogDebug("Registered common metadata pattern: {PatternKey}", patternKey);
        }
    }

    /// <summary>
    /// Get validation metadata pattern (commonly used)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyDictionary<string, object> GetValidationMetadata(string field, object? value = null)
    {
        var key = $"validation:{field}";
        if (TryGetCommonPattern(key) is { } cached)
            return cached;

        return BuildMetadata(
            ("field", field),
            ("value", value ?? "null"),
            ("validation_type", "field_validation"),
            ("timestamp", DateTimeOffset.UtcNow)
        );
    }

    /// <summary>
    /// Get business rule metadata pattern (commonly used)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyDictionary<string, object> GetBusinessRuleMetadata(string rule, string entity)
    {
        var key = $"business_rule:{rule}:{entity}";
        if (TryGetCommonPattern(key) is { } cached)
            return cached;

        return BuildMetadata(
            ("rule", rule),
            ("entity", entity),
            ("rule_type", "business_constraint"),
            ("timestamp", DateTimeOffset.UtcNow)
        );
    }

    /// <summary>
    /// Get external service metadata pattern (commonly used)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyDictionary<string, object> GetExternalServiceMetadata(string service, string operation, int? statusCode = null)
    {
        var key = $"external:{service}:{operation}";
        if (TryGetCommonPattern(key) is { } cached)
            return cached;

        return BuildMetadata(
            ("service", service),
            ("operation", operation),
            ("status_code", statusCode?.ToString() ?? "unknown"),
            ("external_service", true),
            ("timestamp", DateTimeOffset.UtcNow)
        );
    }

    #endregion

    #region Batch Operations

    /// <summary>
    /// Create multiple metadata dictionaries efficiently using pooled resources
    /// </summary>
    public IList<IReadOnlyDictionary<string, object>> CreateBatchMetadata(
        IEnumerable<Action<Dictionary<string, object>>> buildActions)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorMetadataPool));

        var actions = buildActions?.ToList() ?? throw new ArgumentNullException(nameof(buildActions));
        var results = new List<IReadOnlyDictionary<string, object>>(actions.Count);

        foreach (var buildAction in actions)
        {
            var metadata = CreateReadOnlyMetadata(buildAction);
            results.Add(metadata);
        }

        return results;
    }

    /// <summary>
    /// Process metadata with pooled key-value list for intermediate operations
    /// </summary>
    public IReadOnlyDictionary<string, object> ProcessMetadataWithIntermediate(
        IReadOnlyDictionary<string, object>? existing,
        Func<List<KeyValuePair<string, object>>, List<KeyValuePair<string, object>>> processor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ErrorMetadataPool));

        ArgumentNullException.ThrowIfNull(processor);

        var kvList = _keyValueListPool.Get();
        try
        {
            // Add existing metadata to list
            if (existing != null)
            {
                foreach (var kvp in existing)
                {
                    kvList.Add(kvp);
                }
            }

            // Process the list
            var processed = processor(kvList);

            // Convert back to dictionary
            var dictionary = GetDictionary();
            try
            {
                foreach (var kvp in processed)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        dictionary[kvp.Key] = kvp.Value;
                    }
                }

                return new Dictionary<string, object>(dictionary);
            }
            finally
            {
                ReturnDictionary(dictionary);
            }
        }
        finally
        {
            kvList.Clear();
            _keyValueListPool.Return(kvList);
        }
    }

    #endregion

    #region Pool Statistics and Maintenance

    /// <summary>
    /// Get pool statistics for monitoring and diagnostics
    /// </summary>
    public ErrorMetadataPoolStatistics GetStatistics()
    {
        if (_disposed) return new ErrorMetadataPoolStatistics();

        // Derive hits/misses: misses == creations, hits == gets - creations
        var dictMisses = Volatile.Read(ref _pooledObjectsCreated);
        var dictGets = Volatile.Read(ref _totalAllocations);
        var dictHits = Math.Max(0, dictGets - dictMisses);

        return new ErrorMetadataPoolStatistics
        {
            DictionaryPoolHits = dictHits,
            DictionaryPoolMisses = dictMisses,
            DictionaryPoolHitRatio = dictGets > 0 ? (double)dictHits / dictGets : 0.0,
            PatternPoolHits = Volatile.Read(ref _patternPoolHits),
            PatternPoolMisses = Volatile.Read(ref _patternPoolMisses),
            PatternPoolHitRatio = (double)Volatile.Read(ref _patternPoolHits) /
                                  Math.Max(1, Volatile.Read(ref _patternPoolHits) + Volatile.Read(ref _patternPoolMisses)),
            TotalAllocations = Volatile.Read(ref _totalAllocations),
            TotalReturns = Volatile.Read(ref _totalReturns),
            PooledObjectsCreated = Volatile.Read(ref _pooledObjectsCreated),
            CommonPatternsCount = _frozenPatterns.Count,
            MemoryPressure = GC.GetTotalMemory(false),
            LastMaintenanceTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Perform maintenance operations (cleanup, resize, etc.). Manually invoked.
    /// </summary>
    public void PerformMaintenance(object? state = null)
    {
        if (_disposed) return;

        try
        {
            var memoryBefore = GC.GetTotalMemory(false);

            // Force GC if memory pressure is high
            if (memoryBefore > _options.MemoryThresholdBytes)
            {
                GC.Collect(0, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                GC.Collect(1, GCCollectionMode.Optimized);

                var memoryAfter = GC.GetTotalMemory(true);
                var memoryFreed = memoryBefore - memoryAfter;

                _logger.LogInformation("Metadata pool maintenance completed, freed {MemoryFreed} bytes", memoryFreed);
            }

            // Log statistics optionally
            var stats = GetStatistics();
            _logger.LogDebug("Metadata pool stats - Dict hits: {DictHits}, Pattern hits: {PatternHits}, Allocations: {Allocations}",
                stats.DictionaryPoolHits, stats.PatternPoolHits, stats.TotalAllocations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metadata pool maintenance");
        }
    }

    #endregion

    #region Private Methods

    private void InitializeCommonPatterns()
    {
        // Pre-register common metadata patterns (immutable snapshots)
        var common = new Dictionary<string, IReadOnlyDictionary<string, object>>
        {
            ["validation:required"] = new Dictionary<string, object> { ["type"] = "required", ["category"] = "validation" },
            ["validation:format"]   = new Dictionary<string, object> { ["type"] = "format",   ["category"] = "validation" },
            ["validation:range"]    = new Dictionary<string, object> { ["type"] = "range",    ["category"] = "validation" },
            ["business:duplicate"]  = new Dictionary<string, object> { ["type"] = "duplicate",["category"] = "business_rule" },
            ["business:permission"] = new Dictionary<string, object> { ["type"] = "permission",["category"] = "business_rule" },
            ["external:timeout"]    = new Dictionary<string, object> { ["type"] = "timeout",  ["category"] = "external_service" },
            ["external:unavailable"]= new Dictionary<string, object> { ["type"] = "unavailable",["category"] = "external_service" },
            ["system:internal"]     = new Dictionary<string, object> { ["type"] = "internal", ["category"] = "system_error" },
            ["system:configuration"]= new Dictionary<string, object> { ["type"] = "configuration",["category"] = "system_error" }
        };

        foreach (var (key, pattern) in common)
        {
            _frozenPatterns[key] = pattern;
        }

        _logger.LogInformation("Initialized {Count} common metadata patterns", common.Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnPooledObjectCreated()
    {
        Interlocked.Increment(ref _pooledObjectsCreated);
    }

    #endregion

    #region Static Members

    /// <summary>
    /// Shared empty metadata instance to avoid allocations
    /// </summary>
    public static readonly IReadOnlyDictionary<string, object> EmptyMetadata =
        new Dictionary<string, object>();

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _frozenPatterns.Clear();
            _logger.LogInformation("ErrorMetadataPool disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ErrorMetadataPool disposal");
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Pooled object policy for Dictionary instances (counts creations)
/// </summary>
internal sealed class DictionaryPooledObjectPolicy : IPooledObjectPolicy<Dictionary<string, object>>
{
    private readonly ErrorMetadataPoolOptions _options;
    private readonly Action _onCreate;

    public DictionaryPooledObjectPolicy(ErrorMetadataPoolOptions options, Action onCreate)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onCreate = onCreate ?? throw new ArgumentNullException(nameof(onCreate));
    }

    public Dictionary<string, object> Create()
    {
        _onCreate();
        return new Dictionary<string, object>(_options.InitialDictionaryCapacity);
    }

    public bool Return(Dictionary<string, object> obj)
    {
        if (obj.Count > _options.MaxDictionaryCapacity)
            return false; // Don't return very large dictionaries to pool

        obj.Clear();
        return true;
    }
}

/// <summary>
/// Pooled object policy for KeyValuePair List instances (counts creations)
/// </summary>
internal sealed class KeyValueListPooledObjectPolicy : IPooledObjectPolicy<List<KeyValuePair<string, object>>>
{
    private readonly ErrorMetadataPoolOptions _options;
    private readonly Action _onCreate;

    public KeyValueListPooledObjectPolicy(ErrorMetadataPoolOptions options, Action onCreate)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onCreate = onCreate ?? throw new ArgumentNullException(nameof(onCreate));
    }

    public List<KeyValuePair<string, object>> Create()
    {
        _onCreate();
        return new List<KeyValuePair<string, object>>(_options.InitialListCapacity);
    }

    public bool Return(List<KeyValuePair<string, object>> obj)
    {
        if (obj.Count > _options.MaxListCapacity)
            return false; // Don't return very large lists to pool

        obj.Clear();
        return true;
    }
}

/// <summary>
/// Configuration options for ErrorMetadataPool
/// </summary>
public sealed class ErrorMetadataPoolOptions
{
    /// <summary>
    /// Maximum number of objects to retain in each pool (default: 100)
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Initial capacity for pooled dictionaries (default: 8)
    /// </summary>
    public int InitialDictionaryCapacity { get; set; } = 8;

    /// <summary>
    /// Maximum capacity for dictionaries to be returned to pool (default: 32)
    /// </summary>
    public int MaxDictionaryCapacity { get; set; } = 32;

    /// <summary>
    /// Initial capacity for pooled lists (default: 16)
    /// </summary>
    public int InitialListCapacity { get; set; } = 16;

    /// <summary>
    /// Maximum capacity for lists to be returned to pool (default: 64)
    /// </summary>
    public int MaxListCapacity { get; set; } = 64;

    /// <summary>
    /// Maximum number of common patterns to cache (default: 50)
    /// </summary>
    public int MaxCommonPatterns { get; set; } = 50;

    /// <summary>
    /// Maintenance interval hint (legacy). No timer here; keep for compat.
    /// </summary>
    public int MaintenanceIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Memory threshold for triggering cleanup (default: 20MB)
    /// </summary>
    public long MemoryThresholdBytes { get; set; } = 20 * 1024 * 1024;
}

/// <summary>
/// Statistics for ErrorMetadataPool monitoring and diagnostics
/// </summary>
public sealed record ErrorMetadataPoolStatistics
{
    public long DictionaryPoolHits { get; init; }
    public long DictionaryPoolMisses { get; init; }
    public double DictionaryPoolHitRatio { get; init; }
    public long PatternPoolHits { get; init; }
    public long PatternPoolMisses { get; init; }
    public double PatternPoolHitRatio { get; init; }
    public long TotalAllocations { get; init; }
    public long TotalReturns { get; init; }
    public long PooledObjectsCreated { get; init; }
    public int CommonPatternsCount { get; init; }
    public long MemoryPressure { get; init; }
    public DateTimeOffset LastMaintenanceTime { get; init; }
}
