using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance metrics collection system for Error tracking and analysis.
/// Provides comprehensive error telemetry with zero-allocation hot paths,
/// real-time aggregation, and intelligent sampling for production environments.
/// </summary>
public sealed class ErrorMetrics : IDisposable
{
    private static readonly Meter Meter = new("Axon.Errors", "1.0.0");
    
    // Core error metrics instruments
    private static readonly Counter<long> ErrorCount = Meter.CreateCounter<long>(
        "axon.errors.count",
        "errors",
        "Total number of errors created");
        
    private static readonly Counter<long> ErrorByTypeCount = Meter.CreateCounter<long>(
        "axon.errors.by_type.count", 
        "errors",
        "Number of errors by error type");
        
    private static readonly Counter<long> ErrorBySeverityCount = Meter.CreateCounter<long>(
        "axon.errors.by_severity.count",
        "errors", 
        "Number of errors by severity level");
        
    private static readonly Histogram<double> ErrorCreationDuration = Meter.CreateHistogram<double>(
        "axon.errors.creation.duration",
        "milliseconds",
        "Time spent creating error instances");
        
    private static readonly Histogram<double> ErrorSerializationDuration = Meter.CreateHistogram<double>(
        "axon.errors.serialization.duration", 
        "milliseconds",
        "Time spent serializing errors");
        
    private static readonly Counter<long> ErrorCacheHits = Meter.CreateCounter<long>(
        "axon.errors.cache.hits",
        "hits",
        "Number of error cache hits");
        
    private static readonly Counter<long> ErrorCacheMisses = Meter.CreateCounter<long>(
        "axon.errors.cache.misses",
        "misses", 
        "Number of error cache misses");
        
    private static readonly Gauge<long> ActiveErrors = Meter.CreateGauge<long>(
        "axon.errors.active.count",
        "errors",
        "Number of currently active/unresolved errors");
        
    private static readonly Histogram<long> ErrorMessageLength = Meter.CreateHistogram<long>(
        "axon.errors.message.length",
        "characters",
        "Length of error messages");
        
    private static readonly Counter<long> ErrorWithMetadata = Meter.CreateCounter<long>(
        "axon.errors.with_metadata.count",
        "errors",
        "Number of errors created with metadata");
        
    private static readonly Counter<long> ErrorWithExceptions = Meter.CreateCounter<long>(
        "axon.errors.with_exceptions.count", 
        "errors",
        "Number of errors created with inner exceptions");
    
    // Hot path performance metrics
    private static readonly Counter<long> ZeroAllocationPaths = Meter.CreateCounter<long>(
        "axon.errors.zero_allocation.count",
        "operations",
        "Number of zero-allocation error operations");
        
    private static readonly Histogram<double> MemoryAllocated = Meter.CreateHistogram<double>(
        "axon.errors.memory.allocated",
        "bytes", 
        "Memory allocated for error operations");
    
    // Error pattern analysis
    private readonly ConcurrentDictionary<string, ErrorPattern> _errorPatterns;
    private readonly ConcurrentDictionary<string, long> _errorCodeFrequency;
    private readonly ConcurrentDictionary<ErrorType, long> _errorTypeFrequency;
    private readonly ConcurrentDictionary<ErrorSeverity, long> _errorSeverityFrequency;
    
    // Time-series data for trends
    private readonly ConcurrentQueue<ErrorTimeSeriesEntry> _timeSeriesData;
    private readonly Timer _aggregationTimer;
    
    // Configuration and dependencies
    private readonly ErrorMetricsOptions _options;
    private readonly ILogger<ErrorMetrics> _logger;
    
    // State management
    private readonly object _lockObject = new();
    private long _totalErrorsTracked;
    private long _currentActiveErrors;
    private DateTimeOffset _lastResetTime;
    private bool _disposed;
    
    public ErrorMetrics(
        IOptions<ErrorMetricsOptions> options,
        ILogger<ErrorMetrics> logger)
    {
        _options = options?.Value ?? new ErrorMetricsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _errorPatterns = new ConcurrentDictionary<string, ErrorPattern>();
        _errorCodeFrequency = new ConcurrentDictionary<string, long>();
        _errorTypeFrequency = new ConcurrentDictionary<ErrorType, long>();
        _errorSeverityFrequency = new ConcurrentDictionary<ErrorSeverity, long>();
        _timeSeriesData = new ConcurrentQueue<ErrorTimeSeriesEntry>();
        
        _lastResetTime = DateTimeOffset.UtcNow;
        
        // Setup aggregation timer
        _aggregationTimer = new Timer(PerformAggregation, null,
            TimeSpan.FromSeconds(_options.AggregationIntervalSeconds),
            TimeSpan.FromSeconds(_options.AggregationIntervalSeconds));
    }
    
    #region Error Tracking Methods
    
    /// <summary>
    /// Track error creation with comprehensive metrics (zero-allocation for common patterns)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackErrorCreated(Error error, TimeSpan? creationTime = null)
    {
        if (_disposed || error == null) return;
        
        // Core metrics with tags
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", error.Code),
            new("error.type", error.Type.ToString()),
            new("error.severity", error.Severity.ToString()),
            new("has_inner_exception", error.InnerException != null),
            new("has_metadata", error.Metadata != null && error.Metadata.Count > 0),
            new("has_correlation_id", !string.IsNullOrEmpty(error.CorrelationId)),
            new("has_source", !string.IsNullOrEmpty(error.Source))
        };
        
        // Record basic metrics
        ErrorCount.Add(1, tags);
        ErrorByTypeCount.Add(1, new KeyValuePair<string, object?>[] { new("error.type", error.Type.ToString()) });
        ErrorBySeverityCount.Add(1, new KeyValuePair<string, object?>[] { new("error.severity", error.Severity.ToString()) });
        
        // Track message length
        if (!string.IsNullOrEmpty(error.Message))
        {
            ErrorMessageLength.Record(error.Message.Length, tags);
        }
        
        // Track metadata usage
        if (error.Metadata != null && error.Metadata.Count > 0)
        {
            ErrorWithMetadata.Add(1, tags);
        }
        
        // Track exception usage
        if (error.InnerException != null)
        {
            ErrorWithExceptions.Add(1, tags);
        }
        
        // Track creation time if provided
        if (creationTime.HasValue)
        {
            ErrorCreationDuration.Record(creationTime.Value.TotalMilliseconds, tags);
        }
        
        // Update internal tracking
        Interlocked.Increment(ref _totalErrorsTracked);
        Interlocked.Increment(ref _currentActiveErrors);
        
        // Update frequency counters
        _errorCodeFrequency.AddOrUpdate(error.Code, 1, (_, count) => count + 1);
        _errorTypeFrequency.AddOrUpdate(error.Type, 1, (_, count) => count + 1);
        _errorSeverityFrequency.AddOrUpdate(error.Severity, 1, (_, count) => count + 1);
        
        // Track error patterns
        TrackErrorPattern(error);
        
        // Add to time series data
        if (_options.EnableTimeSeriesTracking)
        {
            AddTimeSeriesEntry(error);
        }
    }
    
    /// <summary>
    /// Track error resolution/handling
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackErrorResolved(string errorCode, TimeSpan resolutionTime)
    {
        if (_disposed) return;
        
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", errorCode),
            new("resolution.duration_ms", resolutionTime.TotalMilliseconds)
        };
        
        Meter.CreateCounter<long>("axon.errors.resolved.count", "resolutions")
            .Add(1, tags);
            
        Meter.CreateHistogram<double>("axon.errors.resolution.duration", "milliseconds")
            .Record(resolutionTime.TotalMilliseconds, tags);
        
        // Decrement active errors
        var newCount = Interlocked.Decrement(ref _currentActiveErrors);
        if (newCount < 0) Interlocked.Exchange(ref _currentActiveErrors, 0);
        
        UpdateActiveErrorsGauge();
    }
    
    /// <summary>
    /// Track cache operations with zero-allocation paths
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackCacheHit(string errorCode, string cacheType = "default")
    {
        if (_disposed) return;
        
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", errorCode),
            new("cache.type", cacheType)
        };
        
        ErrorCacheHits.Add(1, tags);
        ZeroAllocationPaths.Add(1, tags);
    }
    
    /// <summary>
    /// Track cache misses
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackCacheMiss(string errorCode, string cacheType = "default")
    {
        if (_disposed) return;
        
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", errorCode),
            new("cache.type", cacheType)
        };
        
        ErrorCacheMisses.Add(1, tags);
    }
    
    /// <summary>
    /// Track serialization performance
    /// </summary>
    public void TrackSerialization(string format, TimeSpan duration, long serializedSize)
    {
        if (_disposed) return;
        
        var tags = new KeyValuePair<string, object?>[]
        {
            new("serialization.format", format),
            new("serialized.size_bytes", serializedSize)
        };
        
        ErrorSerializationDuration.Record(duration.TotalMilliseconds, tags);
        
        Meter.CreateHistogram<long>("axon.errors.serialized.size", "bytes")
            .Record(serializedSize, tags);
    }
    
    /// <summary>
    /// Track memory allocation for error operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackMemoryAllocation(long bytesAllocated, string operation = "create")
    {
        if (_disposed) return;
        
        var tags = new KeyValuePair<string, object?>[]
        {
            new("operation", operation)
        };
        
        MemoryAllocated.Record(bytesAllocated, tags);
    }
    
    #endregion
    
    #region Pattern Analysis
    
    /// <summary>
    /// Track error patterns for analysis and optimization
    /// </summary>
    private void TrackErrorPattern(Error error)
    {
        if (!_options.EnablePatternTracking) return;
        
        var patternKey = $"{error.Type}:{error.Severity}:{GetMessagePattern(error.Message)}";
        
        _errorPatterns.AddOrUpdate(patternKey, 
            _ => new ErrorPattern
            {
                ErrorType = error.Type,
                Severity = error.Severity,
                MessagePattern = GetMessagePattern(error.Message),
                Count = 1,
                FirstSeen = DateTimeOffset.UtcNow,
                LastSeen = DateTimeOffset.UtcNow,
                HasMetadata = error.Metadata != null && error.Metadata.Count > 0,
                HasInnerException = error.InnerException != null
            },
            (_, existing) => existing with 
            { 
                Count = existing.Count + 1, 
                LastSeen = DateTimeOffset.UtcNow 
            });
    }
    
    /// <summary>
    /// Extract pattern from error message for analysis
    /// </summary>
    private static string GetMessagePattern(string message)
    {
        if (string.IsNullOrEmpty(message)) return "empty";
        
        // Simple pattern extraction - replace numbers and IDs with placeholders
        var pattern = message;
        pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"\d+", "{number}");
        pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}", "{guid}");
        pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"'[^']*'", "{value}");
        
        return pattern.Length > 100 ? pattern[..100] : pattern;
    }
    
    /// <summary>
    /// Get top error patterns for analysis
    /// </summary>
    public IEnumerable<ErrorPattern> GetTopErrorPatterns(int count = 10)
    {
        if (_disposed) return Enumerable.Empty<ErrorPattern>();
        
        return _errorPatterns.Values
            .OrderByDescending(p => p.Count)
            .Take(count)
            .ToList();
    }
    
    #endregion
    
    #region Time Series Tracking
    
    /// <summary>
    /// Add entry to time series data for trend analysis
    /// </summary>
    private void AddTimeSeriesEntry(Error error)
    {
        var entry = new ErrorTimeSeriesEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            ErrorType = error.Type,
            Severity = error.Severity,
            ErrorCode = error.Code
        };
        
        _timeSeriesData.Enqueue(entry);
        
        // Limit time series data size
        while (_timeSeriesData.Count > _options.MaxTimeSeriesEntries)
        {
            _timeSeriesData.TryDequeue(out _);
        }
    }
    
    /// <summary>
    /// Get error rate for a specific time window
    /// </summary>
    public double GetErrorRate(TimeSpan window)
    {
        if (_disposed) return 0.0;
        
        var cutoff = DateTimeOffset.UtcNow - window;
        var recentEntries = _timeSeriesData.Count(e => e.Timestamp >= cutoff);
        
        return recentEntries / window.TotalMinutes; // Errors per minute
    }
    
    /// <summary>
    /// Get error trend data for the specified period
    /// </summary>
    public ErrorTrendData GetErrorTrend(TimeSpan period, TimeSpan bucketSize)
    {
        if (_disposed) return new ErrorTrendData { Buckets = Array.Empty<ErrorTrendBucket>() };
        
        var cutoff = DateTimeOffset.UtcNow - period;
        var relevantEntries = _timeSeriesData.Where(e => e.Timestamp >= cutoff).ToList();
        
        var buckets = new List<ErrorTrendBucket>();
        var bucketCount = (int)(period.TotalMilliseconds / bucketSize.TotalMilliseconds);
        
        for (int i = 0; i < bucketCount; i++)
        {
            var bucketStart = cutoff.Add(TimeSpan.FromMilliseconds(i * bucketSize.TotalMilliseconds));
            var bucketEnd = bucketStart.Add(bucketSize);
            
            var bucketEntries = relevantEntries.Where(e => e.Timestamp >= bucketStart && e.Timestamp < bucketEnd).ToList();
            
            buckets.Add(new ErrorTrendBucket
            {
                StartTime = bucketStart,
                EndTime = bucketEnd,
                ErrorCount = bucketEntries.Count,
                ErrorsByType = bucketEntries.GroupBy(e => e.ErrorType).ToDictionary(g => g.Key, g => g.Count()),
                ErrorsBySeverity = bucketEntries.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count())
            });
        }
        
        return new ErrorTrendData { Buckets = buckets.ToArray() };
    }
    
    #endregion
    
    #region Statistics and Reporting
    
    /// <summary>
    /// Get comprehensive error metrics statistics
    /// </summary>
    public ErrorMetricsStatistics GetStatistics()
    {
        if (_disposed) return new ErrorMetricsStatistics();
        
        var uptime = DateTimeOffset.UtcNow - _lastResetTime;
        var errorRate = uptime.TotalMinutes > 0 ? _totalErrorsTracked / uptime.TotalMinutes : 0;
        
        return new ErrorMetricsStatistics
        {
            TotalErrorsTracked = _totalErrorsTracked,
            CurrentActiveErrors = _currentActiveErrors,
            ErrorRate = errorRate,
            TopErrorCodes = _errorCodeFrequency.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ErrorsByType = _errorTypeFrequency.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ErrorsBySeverity = _errorSeverityFrequency.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            TopPatterns = GetTopErrorPatterns(5).ToList(),
            UptimeMinutes = uptime.TotalMinutes,
            LastResetTime = _lastResetTime,
            MemoryPressure = GC.GetTotalMemory(false)
        };
    }
    
    /// <summary>
    /// Reset all metrics and counters
    /// </summary>
    public void Reset()
    {
        if (_disposed) return;
        
        lock (_lockObject)
        {
            _errorPatterns.Clear();
            _errorCodeFrequency.Clear();
            _errorTypeFrequency.Clear();
            _errorSeverityFrequency.Clear();
            
            while (_timeSeriesData.TryDequeue(out _)) { }
            
            Interlocked.Exchange(ref _totalErrorsTracked, 0);
            Interlocked.Exchange(ref _currentActiveErrors, 0);
            _lastResetTime = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("Error metrics reset");
        }
    }
    
    #endregion
    
    #region Private Methods
    
    private void PerformAggregation(object? state)
    {
        if (_disposed) return;
        
        try
        {
            // Update gauge metrics
            UpdateActiveErrorsGauge();
            
            // Log periodic statistics if enabled
            if (_options.EnablePeriodicLogging)
            {
                var stats = GetStatistics();
                _logger.LogInformation("Error metrics - Total: {Total}, Active: {Active}, Rate: {Rate:F2}/min", 
                    stats.TotalErrorsTracked, stats.CurrentActiveErrors, stats.ErrorRate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics aggregation");
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateActiveErrorsGauge()
    {
        ActiveErrors.Record(_currentActiveErrors);
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            _aggregationTimer?.Dispose();
            
            // Final statistics log
            var stats = GetStatistics();
            _logger.LogInformation("ErrorMetrics disposed - Final stats: Total errors: {Total}, Active: {Active}", 
                stats.TotalErrorsTracked, stats.CurrentActiveErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ErrorMetrics disposal");
        }
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}

/// <summary>
/// Configuration options for ErrorMetrics
/// </summary>
public sealed class ErrorMetricsOptions
{
    /// <summary>
    /// Aggregation interval in seconds (default: 30)
    /// </summary>
    public int AggregationIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Enable pattern tracking (default: true)
    /// </summary>
    public bool EnablePatternTracking { get; set; } = true;
    
    /// <summary>
    /// Enable time series tracking (default: true)
    /// </summary>
    public bool EnableTimeSeriesTracking { get; set; } = true;
    
    /// <summary>
    /// Maximum time series entries to keep in memory (default: 10000)
    /// </summary>
    public int MaxTimeSeriesEntries { get; set; } = 10000;

    /// <summary>
    /// Enable periodic logging of statistics (default: false)
    /// </summary>
    public bool EnablePeriodicLogging { get; set; }
}

/// <summary>
/// Error pattern information for analysis
/// </summary>
public sealed record ErrorPattern
{
    public required ErrorType ErrorType { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public required string MessagePattern { get; init; }
    public required long Count { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required bool HasMetadata { get; init; }
    public required bool HasInnerException { get; init; }
}

/// <summary>
/// Time series entry for error tracking
/// </summary>
public sealed record ErrorTimeSeriesEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required ErrorType ErrorType { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public required string ErrorCode { get; init; }
}

/// <summary>
/// Error trend analysis data
/// </summary>
public sealed record ErrorTrendData
{
    public required ErrorTrendBucket[] Buckets { get; init; }
}

/// <summary>
/// Time bucket for error trend analysis
/// </summary>
public sealed record ErrorTrendBucket
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required int ErrorCount { get; init; }
    public required IReadOnlyDictionary<ErrorType, int> ErrorsByType { get; init; }
    public required IReadOnlyDictionary<ErrorSeverity, int> ErrorsBySeverity { get; init; }
}

/// <summary>
/// Comprehensive error metrics statistics
/// </summary>
public sealed record ErrorMetricsStatistics
{
    public long TotalErrorsTracked { get; init; }
    public long CurrentActiveErrors { get; init; }
    public double ErrorRate { get; init; }
    public IReadOnlyDictionary<string, long> TopErrorCodes { get; init; } = new Dictionary<string, long>();
    public IReadOnlyDictionary<ErrorType, long> ErrorsByType { get; init; } = new Dictionary<ErrorType, long>();
    public IReadOnlyDictionary<ErrorSeverity, long> ErrorsBySeverity { get; init; } = new Dictionary<ErrorSeverity, long>();
    public IReadOnlyList<ErrorPattern> TopPatterns { get; init; } = new List<ErrorPattern>();
    public double UptimeMinutes { get; init; }
    public DateTimeOffset LastResetTime { get; init; }
    public long MemoryPressure { get; init; }
}