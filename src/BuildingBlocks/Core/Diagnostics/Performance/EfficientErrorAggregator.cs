using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Performance;

/// <summary>
/// High-performance error aggregation system for collecting, grouping, and summarizing
/// errors with zero-allocation hot paths, intelligent batching, and memory-efficient storage.
/// Optimized for high-throughput scenarios with minimal performance impact.
/// </summary>
public sealed class EfficientErrorAggregator : IDisposable
{
    private readonly OptimizedErrorFactory _errorFactory;
    private readonly ErrorMetrics _errorMetrics;
    private readonly EfficientErrorAggregatorOptions _options;
    private readonly ILogger<EfficientErrorAggregator> _logger;
    
    // High-performance concurrent collections
    private readonly ConcurrentDictionary<ErrorAggregateKey, ErrorAggregate> _aggregates;
    private readonly ConcurrentQueue<PendingError> _pendingErrors;
    private readonly ConcurrentDictionary<string, ErrorGroup> _errorGroups;
    private readonly ConcurrentBag<Error> _recentErrors;
    
    // Background processing
    private readonly Timer _aggregationTimer;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _processingLock;
    
    // Performance tracking
    private long _totalErrorsProcessed;
    private long _aggregationOperations;
    private long _groupingOperations;
    private long _batchProcessingOperations;
    private long _memoryOptimizationOperations;
    
    // State management
    private readonly object _lockObject = new();
    private volatile bool _disposed;
    private DateTimeOffset _lastCleanupTime;
    
    public EfficientErrorAggregator(
        OptimizedErrorFactory errorFactory,
        ErrorMetrics errorMetrics,
        IOptions<EfficientErrorAggregatorOptions> options,
        ILogger<EfficientErrorAggregator> logger)
    {
        _errorFactory = errorFactory ?? throw new ArgumentNullException(nameof(errorFactory));
        _errorMetrics = errorMetrics ?? throw new ArgumentNullException(nameof(errorMetrics));
        _options = options?.Value ?? new EfficientErrorAggregatorOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _aggregates = new ConcurrentDictionary<ErrorAggregateKey, ErrorAggregate>();
        _pendingErrors = new ConcurrentQueue<PendingError>();
        _errorGroups = new ConcurrentDictionary<string, ErrorGroup>();
        _recentErrors = new ConcurrentBag<Error>();
        
        _processingLock = new SemaphoreSlim(1, 1);
        _lastCleanupTime = DateTimeOffset.UtcNow;
        
        // Setup background timers
        _aggregationTimer = new Timer(ProcessAggregation, null,
            TimeSpan.FromMilliseconds(_options.AggregationIntervalMs),
            TimeSpan.FromMilliseconds(_options.AggregationIntervalMs));
            
        _cleanupTimer = new Timer(PerformCleanup, null,
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));
    }
    
    #region Error Aggregation Operations
    
    /// <summary>
    /// Add error to aggregation with zero-allocation hot path for repeated patterns
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AggregateError(Error error)
    {
        if (_disposed || error == null) return;
        
        var key = CreateAggregateKey(error);
        
        // Fast path for existing aggregates (zero allocation)
        if (_aggregates.TryGetValue(key, out var existingAggregate))
        {
            existingAggregate.AddOccurrence(error);
            RecordAggregationOperation();
            return;
        }
        
        // Slower path for new aggregates
        var newAggregate = new ErrorAggregate(key, error, _options.MaxErrorSamples);
        if (_aggregates.TryAdd(key, newAggregate))
        {
            RecordAggregationOperation();
            _errorMetrics.TrackErrorCreated(error);
        }
        else
        {
            // Race condition - try to add to existing aggregate
            if (_aggregates.TryGetValue(key, out var racedAggregate))
            {
                racedAggregate.AddOccurrence(error);
                RecordAggregationOperation();
            }
        }
        
        Interlocked.Increment(ref _totalErrorsProcessed);
        
        // Add to recent errors for immediate access (with size limit)
        if (_recentErrors.Count < _options.MaxRecentErrors)
        {
            _recentErrors.Add(error);
        }
    }
    
    /// <summary>
    /// Batch aggregate multiple errors efficiently
    /// </summary>
    public void AggregateBatch(IEnumerable<Error> errors)
    {
        if (_disposed) return;
        
        var errorList = errors.ToList();
        if (errorList.Count == 0) return;
        
        // Group by aggregate key for efficient processing
        var groupedErrors = errorList.GroupBy(CreateAggregateKey);
        
        foreach (var group in groupedErrors)
        {
            var key = group.Key;
            var errorsInGroup = group.ToList();
            
            if (_aggregates.TryGetValue(key, out var existingAggregate))
            {
                // Batch add to existing aggregate
                existingAggregate.AddBatchOccurrences(errorsInGroup);
            }
            else
            {
                // Create new aggregate with all errors
                var newAggregate = new ErrorAggregate(key, errorsInGroup[0], _options.MaxErrorSamples);
                if (errorsInGroup.Count > 1)
                {
                    newAggregate.AddBatchOccurrences(errorsInGroup.Skip(1));
                }
                _aggregates.TryAdd(key, newAggregate);
            }
        }
        
        Interlocked.Add(ref _totalErrorsProcessed, errorList.Count);
        RecordBatchProcessingOperation();
    }
    
    /// <summary>
    /// Add error to pending queue for background aggregation (minimal overhead)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QueueErrorForAggregation(Error error, string? context = null)
    {
        if (_disposed || error == null) return;
        
        if (_pendingErrors.Count >= _options.MaxPendingErrors)
        {
            // Overflow handling - process immediately to prevent memory issues
            AggregateError(error);
            return;
        }
        
        _pendingErrors.Enqueue(new PendingError
        {
            Error = error,
            QueuedAt = DateTimeOffset.UtcNow,
            Context = context
        });
    }
    
    #endregion
    
    #region Error Grouping and Classification
    
    /// <summary>
    /// Group errors by custom criteria with efficient classification
    /// </summary>
    public void GroupErrors(string groupName, Func<Error, bool> predicate, IEnumerable<Error> errors)
    {
        if (_disposed) return;
        
        var matchingErrors = errors.Where(predicate).ToList();
        if (matchingErrors.Count == 0) return;
        
        var group = _errorGroups.GetOrAdd(groupName, _ => new ErrorGroup(groupName, _options.MaxGroupSize));
        group.AddErrors(matchingErrors);
        
        RecordGroupingOperation();
    }
    
    /// <summary>
    /// Auto-group errors by common patterns
    /// </summary>
    public void AutoGroupErrors()
    {
        if (_disposed) return;
        
        var allErrors = GetRecentErrors();
        
        // Group by error type
        GroupErrors("ByType_Validation", e => e.Type == ErrorType.Validation, allErrors);
        GroupErrors("ByType_NotFound", e => e.Type == ErrorType.NotFound, allErrors);
        GroupErrors("ByType_BusinessRule", e => e.Type == ErrorType.BusinessRule, allErrors);
        GroupErrors("ByType_Internal", e => e.Type == ErrorType.Internal, allErrors);
        
        // Group by severity
        GroupErrors("BySeverity_Critical", e => e.Severity == ErrorSeverity.Critical, allErrors);
        GroupErrors("BySeverity_Error", e => e.Severity == ErrorSeverity.Error, allErrors);
        
        // Group by time patterns
        var now = DateTimeOffset.UtcNow;
        var recentCritical = allErrors.Where(e => 
            e.Severity == ErrorSeverity.Critical && 
            (now - e.OccurredAt).TotalMinutes <= 5);
        
        if (recentCritical.Any())
        {
            GroupErrors("Recent_Critical", _ => true, recentCritical);
        }
    }
    
    #endregion
    
    #region Aggregation Retrieval and Analysis
    
    /// <summary>
    /// Get error aggregates with filtering and sorting
    /// </summary>
    public IEnumerable<ErrorAggregate> GetAggregates(
        ErrorType? type = null,
        ErrorSeverity? minSeverity = null,
        int maxResults = 100,
        AggregateOrderBy orderBy = AggregateOrderBy.Count)
    {
        if (_disposed) return Enumerable.Empty<ErrorAggregate>();
        
        var query = _aggregates.Values.AsEnumerable();
        
        // Apply filters
        if (type.HasValue)
        {
            query = query.Where(a => a.ErrorType == type.Value);
        }
        
        if (minSeverity.HasValue)
        {
            query = query.Where(a => a.Severity >= minSeverity.Value);
        }
        
        // Apply ordering
        query = orderBy switch
        {
            AggregateOrderBy.Count => query.OrderByDescending(a => a.Count),
            AggregateOrderBy.Recent => query.OrderByDescending(a => a.LastOccurrence),
            AggregateOrderBy.Severity => query.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Count),
            _ => query.OrderByDescending(a => a.Count)
        };
        
        return query.Take(maxResults).ToList();
    }
    
    /// <summary>
    /// Get top error patterns for analysis
    /// </summary>
    public IEnumerable<ErrorPattern> GetTopPatterns(int count = 10)
    {
        if (_disposed) return Enumerable.Empty<ErrorPattern>();
        
        return _aggregates.Values
            .OrderByDescending(a => a.Count)
            .Take(count)
            .Select(a => new ErrorPattern
            {
                ErrorType = a.ErrorType,
                Severity = a.Severity,
                MessagePattern = a.MessagePattern,
                Count = a.Count,
                FirstSeen = a.FirstOccurrence,
                LastSeen = a.LastOccurrence,
                HasMetadata = a.HasMetadata,
                HasInnerException = a.HasInnerException
            })
            .ToList();
    }
    
    /// <summary>
    /// Get error summary statistics
    /// </summary>
    public ErrorAggregationSummary GetSummary()
    {
        if (_disposed) return new ErrorAggregationSummary();
        
        var aggregates = _aggregates.Values.ToList();
        var groups = _errorGroups.Values.ToList();
        var totalErrors = aggregates.Sum(a => a.Count);
        
        return new ErrorAggregationSummary
        {
            TotalAggregates = aggregates.Count,
            TotalErrors = totalErrors,
            TotalGroups = groups.Count,
            ErrorsByType = aggregates.GroupBy(a => a.ErrorType).ToDictionary(g => g.Key, g => g.Sum(a => a.Count)),
            ErrorsBySeverity = aggregates.GroupBy(a => a.Severity).ToDictionary(g => g.Key, g => g.Sum(a => a.Count)),
            TopErrorCodes = aggregates
                .OrderByDescending(a => a.Count)
                .Take(10)
                .ToDictionary(a => a.ErrorCode, a => a.Count),
            ProcessingStatistics = new ProcessingStatistics
            {
                TotalErrorsProcessed = _totalErrorsProcessed,
                AggregationOperations = _aggregationOperations,
                GroupingOperations = _groupingOperations,
                BatchProcessingOperations = _batchProcessingOperations,
                MemoryOptimizationOperations = _memoryOptimizationOperations,
                PendingErrorsCount = _pendingErrors.Count,
                RecentErrorsCount = _recentErrors.Count
            }
        };
    }
    
    /// <summary>
    /// Get recent errors (last N errors added)
    /// </summary>
    public IReadOnlyList<Error> GetRecentErrors(int maxCount = 100)
    {
        if (_disposed) return Array.Empty<Error>();
        
        return _recentErrors
            .OrderByDescending(e => e.OccurredAt)
            .Take(maxCount)
            .ToList();
    }
    
    #endregion
    
    #region Error Correlation and Analysis
    
    /// <summary>
    /// Find correlated errors based on timing and patterns
    /// </summary>
    public IEnumerable<ErrorCorrelation> FindCorrelations(TimeSpan timeWindow, int minOccurrences = 2)
    {
        if (_disposed) return Enumerable.Empty<ErrorCorrelation>();
        
        var correlations = new List<ErrorCorrelation>();
        var recentAggregates = _aggregates.Values
            .Where(a => (DateTimeOffset.UtcNow - a.LastOccurrence) <= timeWindow && a.Count >= minOccurrences)
            .ToList();
        
        // Find temporal correlations
        for (int i = 0; i < recentAggregates.Count; i++)
        {
            for (int j = i + 1; j < recentAggregates.Count; j++)
            {
                var aggregate1 = recentAggregates[i];
                var aggregate2 = recentAggregates[j];
                
                var timeDiff = Math.Abs((aggregate1.LastOccurrence - aggregate2.LastOccurrence).TotalMinutes);
                if (timeDiff <= timeWindow.TotalMinutes)
                {
                    var correlation = new ErrorCorrelation
                    {
                        PrimaryError = aggregate1.ToErrorSummary(),
                        CorrelatedError = aggregate2.ToErrorSummary(),
                        CorrelationType = CorrelationType.Temporal,
                        Strength = CalculateCorrelationStrength(aggregate1, aggregate2),
                        TimeWindow = timeWindow
                    };
                    
                    correlations.Add(correlation);
                }
            }
        }
        
        return correlations.Where(c => c.Strength >= _options.MinCorrelationStrength);
    }
    
    /// <summary>
    /// Create aggregate error from multiple similar errors
    /// </summary>
    public Error CreateAggregateError(IEnumerable<Error> similarErrors)
    {
        if (_disposed) return _errorFactory.CreateInternalError("Aggregator disposed");
        
        var errorList = similarErrors.ToList();
        if (errorList.Count == 0) return _errorFactory.CreateInternalError("No errors to aggregate");
        
        var primaryError = errorList.First();
        var errorCount = errorList.Count;
        
        var aggregateBuilder = _errorFactory.CreateBuilder()
            .WithCode($"AGGREGATE_{primaryError.Code}")
            .WithMessage($"Aggregated error: {primaryError.Message} (occurred {errorCount} times)")
            .WithType(primaryError.Type)
            .WithSeverity(primaryError.Severity)
            .WithMetadata(metadata =>
            {
                metadata["aggregate_count"] = errorCount;
                metadata["first_occurrence"] = errorList.Min(e => e.OccurredAt);
                metadata["last_occurrence"] = errorList.Max(e => e.OccurredAt);
                metadata["error_codes"] = errorList.Select(e => e.Code).Distinct().ToList();
                metadata["unique_sources"] = errorList.Where(e => !string.IsNullOrEmpty(e.Source))
                                                     .Select(e => e.Source)
                                                     .Distinct()
                                                     .ToList();
            });
        
        return aggregateBuilder.Build();
    }
    
    #endregion
    
    #region Background Processing
    
    private async void ProcessAggregation(object? state)
    {
        if (_disposed) return;
        
        if (!await _processingLock.WaitAsync(100)) return; // Non-blocking
        
        try
        {
            // Process pending errors
            var processedCount = 0;
            while (_pendingErrors.TryDequeue(out var pendingError) && processedCount < _options.MaxBatchSize)
            {
                AggregateError(pendingError.Error);
                processedCount++;
            }
            
            if (processedCount > 0)
            {
                _logger.LogDebug("Processed {Count} pending errors", processedCount);
            }
            
            // Auto-group errors periodically
            if (_options.EnableAutoGrouping)
            {
                AutoGroupErrors();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during aggregation processing");
        }
        finally
        {
            _processingLock.Release();
        }
    }
    
    private void PerformCleanup(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cleanupThreshold = now - TimeSpan.FromMinutes(_options.AggregateRetentionMinutes);
            
            // Cleanup old aggregates
            var oldAggregates = _aggregates.Where(kvp => kvp.Value.LastOccurrence < cleanupThreshold).ToList();
            foreach (var (key, _) in oldAggregates)
            {
                _aggregates.TryRemove(key, out _);
            }
            
            // Cleanup old groups
            var oldGroups = _errorGroups.Where(kvp => (now - kvp.Value.CreatedAt).TotalMinutes > _options.GroupRetentionMinutes).ToList();
            foreach (var (key, _) in oldGroups)
            {
                _errorGroups.TryRemove(key, out _);
            }
            
            // Cleanup recent errors
            if (_recentErrors.Count > _options.MaxRecentErrors)
            {
                var errorsToRemove = _recentErrors.Count - _options.MaxRecentErrors;
                for (int i = 0; i < errorsToRemove; i++)
                {
                    _recentErrors.TryTake(out _);
                }
            }
            
            RecordMemoryOptimizationOperation();
            
            if (oldAggregates.Count > 0 || oldGroups.Count > 0)
            {
                _logger.LogInformation("Cleaned up {AggregateCount} old aggregates and {GroupCount} old groups",
                    oldAggregates.Count, oldGroups.Count);
            }
            
            _lastCleanupTime = now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }
    
    #endregion
    
    #region Private Helper Methods
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ErrorAggregateKey CreateAggregateKey(Error error)
    {
        return new ErrorAggregateKey(
            error.Code,
            error.Type,
            error.Severity,
            GetMessagePattern(error.Message),
            !string.IsNullOrEmpty(error.Source) ? error.Source : "unknown",
            error.Metadata?.Count > 0,
            error.InnerException != null
        );
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetMessagePattern(string message)
    {
        if (string.IsNullOrEmpty(message)) return "empty";
        
        // Fast pattern extraction for common cases
        if (message.Length <= 50) return message;
        
        // Simple pattern - take first part and replace common variable patterns
        var pattern = message[..Math.Min(100, message.Length)];
        if (pattern.Contains("'") || pattern.Contains('"'))
        {
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"['""][^'""]*['""]", "{value}");
        }
        
        return pattern;
    }
    
    private double CalculateCorrelationStrength(ErrorAggregate aggregate1, ErrorAggregate aggregate2)
    {
        // Simple correlation strength calculation
        var timeSimilarity = 1.0 / (1.0 + Math.Abs((aggregate1.LastOccurrence - aggregate2.LastOccurrence).TotalMinutes));
        var countSimilarity = Math.Min(aggregate1.Count, aggregate2.Count) / (double)Math.Max(aggregate1.Count, aggregate2.Count);
        var typeSimilarity = aggregate1.ErrorType == aggregate2.ErrorType ? 1.0 : 0.5;
        
        return (timeSimilarity + countSimilarity + typeSimilarity) / 3.0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordAggregationOperation() => Interlocked.Increment(ref _aggregationOperations);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordGroupingOperation() => Interlocked.Increment(ref _groupingOperations);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordBatchProcessingOperation() => Interlocked.Increment(ref _batchProcessingOperations);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordMemoryOptimizationOperation() => Interlocked.Increment(ref _memoryOptimizationOperations);
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            _aggregationTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _processingLock?.Dispose();
            
            var summary = GetSummary();
            _logger.LogInformation("EfficientErrorAggregator disposed - Final summary: {TotalErrors} errors in {TotalAggregates} aggregates",
                summary.TotalErrors, summary.TotalAggregates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during EfficientErrorAggregator disposal");
        }
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}

/// <summary>
/// Thread-safe error aggregate with optimized storage
/// </summary>
public sealed class ErrorAggregate
{
    private readonly object _lockObject = new();
    private readonly List<Error> _sampleErrors;
    private readonly int _maxSamples;
    
    private long _count = 1;
    private DateTimeOffset _lastOccurrence;
    
    public ErrorAggregateKey Key { get; }
    public string ErrorCode { get; }
    public ErrorType ErrorType { get; }
    public ErrorSeverity Severity { get; }
    public string MessagePattern { get; }
    public string Source { get; }
    public bool HasMetadata { get; }
    public bool HasInnerException { get; }
    public DateTimeOffset FirstOccurrence { get; }
    
    public long Count => _count;
    public DateTimeOffset LastOccurrence => _lastOccurrence;
    public IReadOnlyList<Error> SampleErrors
    {
        get
        {
            lock (_lockObject)
            {
                return _sampleErrors.ToList();
            }
        }
    }
    
    internal ErrorAggregate(ErrorAggregateKey key, Error firstError, int maxSamples)
    {
        Key = key;
        ErrorCode = firstError.Code;
        ErrorType = firstError.Type;
        Severity = firstError.Severity;
        MessagePattern = key.MessagePattern;
        Source = key.Source;
        HasMetadata = key.HasMetadata;
        HasInnerException = key.HasInnerException;
        FirstOccurrence = firstError.OccurredAt;
        _lastOccurrence = firstError.OccurredAt;
        
        _maxSamples = maxSamples;
        _sampleErrors = new List<Error> { firstError };
    }
    
    internal void AddOccurrence(Error error)
    {
        Interlocked.Increment(ref _count);
        
        lock (_lockObject)
        {
            _lastOccurrence = error.OccurredAt;
            
            // Keep sample of errors for analysis
            if (_sampleErrors.Count < _maxSamples)
            {
                _sampleErrors.Add(error);
            }
            else if (_sampleErrors.Count > 0)
            {
                // Replace oldest sample
                var oldestIndex = 0;
                for (int i = 1; i < _sampleErrors.Count; i++)
                {
                    if (_sampleErrors[i].OccurredAt < _sampleErrors[oldestIndex].OccurredAt)
                    {
                        oldestIndex = i;
                    }
                }
                _sampleErrors[oldestIndex] = error;
            }
        }
    }
    
    internal void AddBatchOccurrences(IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();
        Interlocked.Add(ref _count, errorList.Count);
        
        lock (_lockObject)
        {
            var latestError = errorList.OrderByDescending(e => e.OccurredAt).First();
            _lastOccurrence = latestError.OccurredAt;
            
            foreach (var error in errorList.Take(_maxSamples - _sampleErrors.Count))
            {
                _sampleErrors.Add(error);
            }
        }
    }
    
    public ErrorSummary ToErrorSummary()
    {
        return new ErrorSummary
        {
            Code = ErrorCode,
            Type = ErrorType,
            Severity = Severity,
            MessagePattern = MessagePattern,
            Count = Count,
            FirstSeen = FirstOccurrence,
            LastSeen = LastOccurrence,
            Source = Source
        };
    }
}

/// <summary>
/// Configuration options for EfficientErrorAggregator
/// </summary>
public sealed class EfficientErrorAggregatorOptions
{
    /// <summary>
    /// Aggregation processing interval in milliseconds (default: 1000)
    /// </summary>
    public int AggregationIntervalMs { get; set; } = 1000;
    
    /// <summary>
    /// Cleanup interval in minutes (default: 5)
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Maximum number of error samples to keep per aggregate (default: 10)
    /// </summary>
    public int MaxErrorSamples { get; set; } = 10;
    
    /// <summary>
    /// Maximum number of recent errors to keep in memory (default: 1000)
    /// </summary>
    public int MaxRecentErrors { get; set; } = 1000;
    
    /// <summary>
    /// Maximum number of pending errors in queue (default: 5000)
    /// </summary>
    public int MaxPendingErrors { get; set; } = 5000;
    
    /// <summary>
    /// Maximum batch size for processing (default: 100)
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Maximum size for error groups (default: 500)
    /// </summary>
    public int MaxGroupSize { get; set; } = 500;
    
    /// <summary>
    /// Aggregate retention period in minutes (default: 60)
    /// </summary>
    public int AggregateRetentionMinutes { get; set; } = 60;
    
    /// <summary>
    /// Group retention period in minutes (default: 30)
    /// </summary>
    public int GroupRetentionMinutes { get; set; } = 30;
    
    /// <summary>
    /// Minimum correlation strength for correlations (default: 0.5)
    /// </summary>
    public double MinCorrelationStrength { get; set; } = 0.5;
    
    /// <summary>
    /// Enable automatic error grouping (default: true)
    /// </summary>
    public bool EnableAutoGrouping { get; set; } = true;
}

/// <summary>
/// Aggregate key for efficient grouping and caching
/// </summary>
public readonly record struct ErrorAggregateKey(
    string ErrorCode,
    ErrorType ErrorType,
    ErrorSeverity Severity,
    string MessagePattern,
    string Source,
    bool HasMetadata,
    bool HasInnerException);

/// <summary>
/// Pending error for background processing
/// </summary>
internal sealed record PendingError
{
    public required Error Error { get; init; }
    public DateTimeOffset QueuedAt { get; init; }
    public string? Context { get; init; }
}

/// <summary>
/// Error group for classification and analysis
/// </summary>
public sealed class ErrorGroup
{
    private readonly object _lockObject = new();
    private readonly List<Error> _errors;
    private readonly int _maxSize;
    
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public int Count
    {
        get
        {
            lock (_lockObject)
            {
                return _errors.Count;
            }
        }
    }
    
    internal ErrorGroup(string name, int maxSize)
    {
        Name = name;
        _maxSize = maxSize;
        CreatedAt = DateTimeOffset.UtcNow;
        _errors = new List<Error>();
    }
    
    internal void AddErrors(IEnumerable<Error> errors)
    {
        lock (_lockObject)
        {
            foreach (var error in errors.Take(_maxSize - _errors.Count))
            {
                _errors.Add(error);
            }
        }
    }
    
    public IReadOnlyList<Error> GetErrors()
    {
        lock (_lockObject)
        {
            return _errors.ToList();
        }
    }
}

/// <summary>
/// Error aggregation summary for reporting
/// </summary>
public sealed record ErrorAggregationSummary
{
    public int TotalAggregates { get; init; }
    public long TotalErrors { get; init; }
    public int TotalGroups { get; init; }
    public IReadOnlyDictionary<ErrorType, long> ErrorsByType { get; init; } = new Dictionary<ErrorType, long>();
    public IReadOnlyDictionary<ErrorSeverity, long> ErrorsBySeverity { get; init; } = new Dictionary<ErrorSeverity, long>();
    public IReadOnlyDictionary<string, long> TopErrorCodes { get; init; } = new Dictionary<string, long>();
    public ProcessingStatistics ProcessingStatistics { get; init; } = new ProcessingStatistics();
}

/// <summary>
/// Processing statistics for monitoring
/// </summary>
public sealed record ProcessingStatistics
{
    public long TotalErrorsProcessed { get; init; }
    public long AggregationOperations { get; init; }
    public long GroupingOperations { get; init; }
    public long BatchProcessingOperations { get; init; }
    public long MemoryOptimizationOperations { get; init; }
    public int PendingErrorsCount { get; init; }
    public int RecentErrorsCount { get; init; }
}

/// <summary>
/// Error correlation analysis result
/// </summary>
public sealed record ErrorCorrelation
{
    public required ErrorSummary PrimaryError { get; init; }
    public required ErrorSummary CorrelatedError { get; init; }
    public required CorrelationType CorrelationType { get; init; }
    public required double Strength { get; init; }
    public required TimeSpan TimeWindow { get; init; }
}

/// <summary>
/// Error summary for correlations and reporting
/// </summary>
public sealed record ErrorSummary
{
    public required string Code { get; init; }
    public required ErrorType Type { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public required string MessagePattern { get; init; }
    public required long Count { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required string Source { get; init; }
}

/// <summary>
/// Types of error correlations
/// </summary>
public enum CorrelationType
{
    Temporal,
    Causal,
    Pattern,
    Source
}

/// <summary>
/// Aggregate ordering options
/// </summary>
public enum AggregateOrderBy
{
    Count,
    Recent,
    Severity
}

// Note: ErrorPattern is defined in ErrorMetrics.cs to avoid duplication