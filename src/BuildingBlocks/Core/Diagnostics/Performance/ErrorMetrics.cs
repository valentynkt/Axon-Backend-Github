using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance metrics collection system for Error tracking and analysis.
/// Provides comprehensive error telemetry with zero-allocation leaning hot paths,
/// real-time aggregation, and intelligent sampling for production environments.
/// </summary>
public sealed class ErrorMetrics : IDisposable
{
    // Single meter & instruments (created once)
    private static readonly Meter Meter = new("Axon.Errors", "1.1.0");

    private static readonly Counter<long> ErrorsCreated = Meter.CreateCounter<long>(
        "axon.errors.created",
        unit: "errors",
        description: "Total number of errors created");

    private static readonly Counter<long> ErrorsByType = Meter.CreateCounter<long>(
        "axon.errors.by_type",
        unit: "errors",
        description: "Number of errors, by error type");

    private static readonly Counter<long> ErrorsBySeverity = Meter.CreateCounter<long>(
        "axon.errors.by_severity",
        unit: "errors",
        description: "Number of errors, by severity");

    private static readonly Histogram<double> ErrorCreationDurationMs = Meter.CreateHistogram<double>(
        "axon.errors.creation.ms",
        unit: "ms",
        description: "Elapsed time when creating error instances");

    private static readonly Histogram<double> ErrorSerializationDurationMs = Meter.CreateHistogram<double>(
        "axon.errors.serialization.ms",
        unit: "ms",
        description: "Elapsed time for serializing errors");

    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "axon.errors.cache.hits",
        unit: "hits",
        description: "Error cache hits");

    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "axon.errors.cache.misses",
        unit: "misses",
        description: "Error cache misses");

    // Active error count over time
    private static readonly UpDownCounter<long> ActiveErrorsCounter = Meter.CreateUpDownCounter<long>(
        "axon.errors.active",
        unit: "errors",
        description: "Currently active/unresolved errors");

    private static readonly Histogram<long> MessageLength = Meter.CreateHistogram<long>(
        "axon.errors.message.len",
        unit: "chars",
        description: "Length of error messages");

    private static readonly Counter<long> WithMetadata = Meter.CreateCounter<long>(
        "axon.errors.with_metadata",
        unit: "errors",
        description: "Errors created with metadata");

    private static readonly Counter<long> WithExceptions = Meter.CreateCounter<long>(
        "axon.errors.with_exception",
        unit: "errors",
        description: "Errors created with inner exceptions");

    private static readonly Counter<long> ZeroAllocPaths = Meter.CreateCounter<long>(
        "axon.errors.zero_alloc",
        unit: "ops",
        description: "Zero-allocation hot-path operations");

    private static readonly Histogram<double> BytesAllocated = Meter.CreateHistogram<double>(
        "axon.errors.mem.bytes",
        unit: "bytes",
        description: "Bytes allocated for error operations");

    // Compiled regexes for pattern extraction
    private static readonly Regex GuidRegex = new(
        @"\b[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NumberRegex = new(
        @"\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedRegex = new(
        @"'[^']*'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Error pattern and frequency tracking (in-memory)
    private readonly ConcurrentDictionary<string, ErrorPattern> _patterns = new();
    private readonly ConcurrentDictionary<string, long> _codeFreq = new();
    private readonly ConcurrentDictionary<ErrorType, long> _typeFreq = new();
    private readonly ConcurrentDictionary<ErrorSeverity, long> _severityFreq = new();

    // Simple time-series (bounded)
    private readonly ConcurrentQueue<ErrorTimeSeriesEntry> _series = new();

    // Aggregation tick
    private readonly Timer _aggTimer;

    // Config & logging
    private readonly ErrorMetricsOptions _options;
    private readonly ILogger<ErrorMetrics> _logger;

    // State
    private long _totalTracked;
    private long _activeErrors; // mirrors ActiveErrorsCounter
    private DateTimeOffset _lastResetUtc = DateTimeOffset.UtcNow;
    private volatile bool _disposed;

    public ErrorMetrics(
        IOptions<ErrorMetricsOptions> options,
        ILogger<ErrorMetrics> logger)
    {
        _options = options?.Value ?? new ErrorMetricsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _aggTimer = new Timer(PerformAggregation,
            state: null,
            dueTime: TimeSpan.FromSeconds(_options.AggregationIntervalSeconds),
            period: TimeSpan.FromSeconds(_options.AggregationIntervalSeconds));
    }

    #region Tracking

    /// <summary>
    /// Track error creation with comprehensive metrics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackErrorCreated(Error error, TimeSpan? creationTime = null)
    {
        if (_disposed || error is null) return;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.code", error.Code),
            new("error.type", error.Type.ToString()),
            new("error.severity", error.Severity.ToString()),
            new("has_exception", error.InnerException is not null),
            new("has_metadata", error.Metadata is { Count: > 0 }),
            new("has_correlation_id", !string.IsNullOrEmpty(error.CorrelationId)),
            new("has_source", !string.IsNullOrEmpty(error.Source))
        };

        // Instruments
        ErrorsCreated.Add(1, tags);
        ErrorsByType.Add(1, new KeyValuePair<string, object?>[] { new("error.type", error.Type.ToString()) });
        ErrorsBySeverity.Add(1, new KeyValuePair<string, object?>[] { new("error.severity", error.Severity.ToString()) });

        if (!string.IsNullOrEmpty(error.Message))
            MessageLength.Record(error.Message.Length, tags);

        if (error.Metadata is { Count: > 0 })
            WithMetadata.Add(1, tags);

        if (error.InnerException is not null)
            WithExceptions.Add(1, tags);

        if (creationTime is { } ct)
            ErrorCreationDurationMs.Record(ct.TotalMilliseconds, tags);

        // Internal counters
        Interlocked.Increment(ref _totalTracked);
        var activeNow = Interlocked.Increment(ref _activeErrors);
        ActiveErrorsCounter.Add(1);

        _codeFreq.AddOrUpdate(error.Code, 1, static (_, v) => v + 1);
        _typeFreq.AddOrUpdate(error.Type, 1, static (_, v) => v + 1);
        _severityFreq.AddOrUpdate(error.Severity, 1, static (_, v) => v + 1);

        if (_options.EnablePatternTracking)
            TrackPattern(error);

        if (_options.EnableTimeSeriesTracking)
            EnqueueTimeSeries(error);
    }

    /// <summary>
    /// Track resolution/handling of an error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackErrorResolved(string errorCode, TimeSpan resolutionTime)
    {
        if (_disposed) return;

        // Resolution counters
        Meter.CreateCounter<long>("axon.errors.resolved", unit: "resolutions")
            .Add(1, new KeyValuePair<string, object?>[] { new("error.code", errorCode) });

        Meter.CreateHistogram<double>("axon.errors.resolution.ms", unit: "ms")
            .Record(resolutionTime.TotalMilliseconds);

        // Active errors down
        var after = Interlocked.Decrement(ref _activeErrors);
        if (after < 0) Interlocked.Exchange(ref _activeErrors, 0);
        ActiveErrorsCounter.Add(-1);
    }

    /// <summary>
    /// Track cache hit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackCacheHit(string errorCode, string cache = "default")
    {
        if (_disposed) return;
        CacheHits.Add(1, new KeyValuePair<string, object?>[]
        {
            new("error.code", errorCode),
            new("cache", cache)
        });
        ZeroAllocPaths.Add(1);
    }

    /// <summary>
    /// Track cache miss.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackCacheMiss(string errorCode, string cache = "default")
    {
        if (_disposed) return;
        CacheMisses.Add(1, new KeyValuePair<string, object?>[]
        {
            new("error.code", errorCode),
            new("cache", cache)
        });
    }

    /// <summary>
    /// Track serialization cost.
    /// </summary>
    public void TrackSerialization(string format, TimeSpan duration, long sizeBytes)
    {
        if (_disposed) return;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("format", format),
            new("size_bytes", sizeBytes)
        };

        ErrorSerializationDurationMs.Record(duration.TotalMilliseconds, tags);
        Meter.CreateHistogram<long>("axon.errors.serialized.bytes", unit: "bytes")
            .Record(sizeBytes, tags);
    }

    /// <summary>
    /// Track memory allocation (external observations).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackMemoryAllocation(long bytes, string operation = "create")
    {
        if (_disposed) return;
        BytesAllocated.Record(bytes, new KeyValuePair<string, object?>[]
        {
            new("op", operation)
        });
    }

    #endregion

    #region Patterns & Series

    private void TrackPattern(Error error)
    {
        var msgPattern = ExtractMessagePattern(error.Message);
        var key = $"{error.Type}:{error.Severity}:{msgPattern}";

        _patterns.AddOrUpdate(
            key,
            _ => new ErrorPattern
            {
                ErrorType = error.Type,
                Severity = error.Severity,
                MessagePattern = msgPattern,
                Count = 1,
                FirstSeen = DateTimeOffset.UtcNow,
                LastSeen = DateTimeOffset.UtcNow,
                HasMetadata = error.Metadata is { Count: > 0 },
                HasInnerException = error.InnerException is not null
            },
            (_, existing) => existing with
            {
                Count = existing.Count + 1,
                LastSeen = DateTimeOffset.UtcNow
            });
    }

    private static string ExtractMessagePattern(string message)
    {
        if (string.IsNullOrEmpty(message)) return "empty";

        // replace GUIDs, numbers, quoted segments
        var s = GuidRegex.Replace(message, "{guid}");
        s = NumberRegex.Replace(s, "{number}");
        s = QuotedRegex.Replace(s, "{value}");

        return s.Length > 100 ? s[..100] : s;
    }

    private void EnqueueTimeSeries(Error error)
    {
        _series.Enqueue(new ErrorTimeSeriesEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            ErrorType = error.Type,
            Severity = error.Severity,
            ErrorCode = error.Code
        });

        while (_series.Count > _options.MaxTimeSeriesEntries)
            _series.TryDequeue(out _);
    }

    public IEnumerable<ErrorPattern> GetTopErrorPatterns(int count = 10)
    {
        if (_disposed) return Enumerable.Empty<ErrorPattern>();
        return _patterns.Values.OrderByDescending(p => p.Count).Take(count).ToList();
    }

    #endregion

    #region Rates & Trends

    public double GetErrorRate(TimeSpan window)
    {
        if (_disposed) return 0.0;

        var since = DateTimeOffset.UtcNow - window;
        var n = _series.Count(e => e.Timestamp >= since);
        var minutes = Math.Max(0.000001, window.TotalMinutes);
        return n / minutes;
    }

    public ErrorTrendData GetErrorTrend(TimeSpan period, TimeSpan bucket)
    {
        if (_disposed) return new ErrorTrendData { Buckets = Array.Empty<ErrorTrendBucket>() };

        var cutoff = DateTimeOffset.UtcNow - period;
        var entries = _series.Where(e => e.Timestamp >= cutoff).ToList();

        var bucketCount = Math.Max(1, (int)(period.TotalMilliseconds / Math.Max(1, bucket.TotalMilliseconds)));
        var buckets = new List<ErrorTrendBucket>(bucketCount);

        for (var i = 0; i < bucketCount; i++)
        {
            var start = cutoff.AddMilliseconds(i * bucket.TotalMilliseconds);
            var end = start.Add(bucket);

            var slice = entries.Where(e => e.Timestamp >= start && e.Timestamp < end).ToList();

            buckets.Add(new ErrorTrendBucket
            {
                StartTime = start,
                EndTime = end,
                ErrorCount = slice.Count,
                ErrorsByType = slice.GroupBy(e => e.ErrorType).ToDictionary(g => g.Key, g => g.Count()),
                ErrorsBySeverity = slice.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count())
            });
        }

        return new ErrorTrendData { Buckets = buckets.ToArray() };
    }

    #endregion

    #region Aggregation & Statistics

    private void PerformAggregation(object? _)
    {
        if (_disposed) return;

        try
        {
            if (_options.EnablePeriodicLogging)
            {
                var stats = GetStatistics();
                _logger.LogInformation("Error metrics - total: {Total}, active: {Active}, rate: {Rate:F2}/min",
                    stats.TotalErrorsTracked, stats.CurrentActiveErrors, stats.ErrorRate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics aggregation");
        }
    }

    public ErrorMetricsStatistics GetStatistics()
    {
        if (_disposed) return new ErrorMetricsStatistics();

        var uptime = DateTimeOffset.UtcNow - _lastResetUtc;
        var tracked = Volatile.Read(ref _totalTracked);
        var active = Volatile.Read(ref _activeErrors);
        var rate = uptime.TotalMinutes > 0 ? tracked / uptime.TotalMinutes : 0;

        return new ErrorMetricsStatistics
        {
            TotalErrorsTracked = tracked,
            CurrentActiveErrors = active,
            ErrorRate = rate,
            TopErrorCodes = _codeFreq.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value),
            ErrorsByType = _typeFreq.ToDictionary(kv => kv.Key, kv => kv.Value),
            ErrorsBySeverity = _severityFreq.ToDictionary(kv => kv.Key, kv => kv.Value),
            TopPatterns = GetTopErrorPatterns(5).ToList(),
            UptimeMinutes = uptime.TotalMinutes,
            LastResetTime = _lastResetUtc,
            MemoryPressure = GC.GetTotalMemory(false)
        };
    }

    public void Reset()
    {
        if (_disposed) return;

        _patterns.Clear();
        _codeFreq.Clear();
        _typeFreq.Clear();
        _severityFreq.Clear();
        while (_series.TryDequeue(out _)) { }

        Interlocked.Exchange(ref _totalTracked, 0);
        // keep _activeErrors as-is; it reflects real-time unresolved count
        _lastResetUtc = DateTimeOffset.UtcNow;

        _logger.LogInformation("Error metrics reset");
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _aggTimer.Dispose();
            var stats = GetStatistics();
            _logger.LogInformation("ErrorMetrics disposed - final: total={Total}, active={Active}",
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
    public int AggregationIntervalSeconds { get; set; } = 30;
    public bool EnablePatternTracking { get; set; } = true;
    public bool EnableTimeSeriesTracking { get; set; } = true;
    public int MaxTimeSeriesEntries { get; set; } = 10_000;
    public bool EnablePeriodicLogging { get; set; }
}

/// <summary> Error pattern information for analysis </summary>
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

/// <summary> Time series entry for error tracking </summary>
public sealed record ErrorTimeSeriesEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required ErrorType ErrorType { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public required string ErrorCode { get; init; }
}

/// <summary> Error trend analysis data </summary>
public sealed record ErrorTrendData
{
    public required ErrorTrendBucket[] Buckets { get; init; }
}

/// <summary> Time bucket for error trend analysis </summary>
public sealed record ErrorTrendBucket
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required int ErrorCount { get; init; }
    public required IReadOnlyDictionary<ErrorType, int> ErrorsByType { get; init; }
    public required IReadOnlyDictionary<ErrorSeverity, int> ErrorsBySeverity { get; init; }
}

/// <summary> Comprehensive error metrics statistics </summary>
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
