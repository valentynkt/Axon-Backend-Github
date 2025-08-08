using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Metrics collection extensions for Result&lt;T&gt; patterns.
/// Provides comprehensive metrics for success rates, failure patterns,
/// performance tracking, and business intelligence with zero-allocation paths.
/// </summary>
public static class ResultMetricsExtensions
{
    private static readonly Meter Meter = new("Axon.Results", "1.0.0");
    
    // Core metrics instruments
    private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
        "axon.result.operations.count",
        "operations",
        "Total number of Result operations");
        
    private static readonly Counter<long> SuccessCounter = Meter.CreateCounter<long>(
        "axon.result.success.count", 
        "successes",
        "Number of successful Result operations");
        
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>(
        "axon.result.failure.count",
        "failures", 
        "Number of failed Result operations");
        
    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "axon.result.operation.duration",
        "milliseconds",
        "Duration of Result operations");
        
    private static readonly Histogram<double> ErrorResolutionTime = Meter.CreateHistogram<double>(
        "axon.result.error.resolution_time", 
        "milliseconds",
        "Time to resolve errors");
        
    private static readonly Counter<long> ErrorTypeCounter = Meter.CreateCounter<long>(
        "axon.result.error_type.count",
        "errors",
        "Number of errors by type");
        
    private static readonly UpDownCounter<long> ActiveOperations = Meter.CreateUpDownCounter<long>(
        "axon.result.active_operations",
        "operations", 
        "Number of currently active operations");
        
    private static readonly Gauge<double> SuccessRate = Meter.CreateGauge<double>(
        "axon.result.success_rate",
        "percentage",
        "Success rate percentage over time window");
        
    private static readonly Histogram<long> RetryAttempts = Meter.CreateHistogram<long>(
        "axon.result.retry.attempts",
        "attempts",
        "Number of retry attempts for operations");
    
    #region Core Metrics Extensions
    
    /// <summary>
    /// Record Result metrics with operation context
    /// </summary>
    public static Result<T> RecordMetrics<T>(
        this Result<T> result,
        string operationName,
        TimeSpan? duration = null,
        IDictionary<string, object?>? tags = null,
        [CallerMemberName] string? memberName = null)
    {
        var allTags = CreateBaseTags(operationName, memberName, typeof(T).Name);
        
        // Add custom tags
        if (tags != null)
        {
            foreach (var (key, value) in tags)
            {
                allTags[key] = value;
            }
        }
        
        // Record core metrics
        OperationCounter.Add(1, allTags);
        
        if (result.IsSuccess)
        {
            SuccessCounter.Add(1, allTags);
        }
        else
        {
            var error = result.Error;
            var errorTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("error.code", error.Code),
                new("error.type", error.Type.ToString()),
                new("error.severity", error.Severity.ToString()),
                new("http.status_code", error.ToHttpStatusCode()),
                new("caller.member", memberName ?? "Unknown"),
                new("result.type", typeof(T).Name)
            };
            
            FailureCounter.Add(1, allTags);
            ErrorTypeCounter.Add(1, errorTags);
        }
        
        // Record duration if provided
        if (duration.HasValue)
        {
            OperationDuration.Record(duration.Value.TotalMilliseconds, allTags);
        }
        
        return result;
    }
    
    /// <summary>
    /// Record Result metrics with Stopwatch timing
    /// </summary>
    public static Result<T> RecordMetrics<T>(
        this Result<T> result,
        string operationName,
        Stopwatch stopwatch,
        IDictionary<string, object?>? tags = null,
        [CallerMemberName] string? memberName = null)
    {
        return result.RecordMetrics(operationName, stopwatch.Elapsed, tags, memberName);
    }
    
    /// <summary>
    /// Create a metrics scope that automatically records operation metrics
    /// </summary>
    public static ResultMetricsScope<T> CreateMetricsScope<T>(
        string operationName,
        IDictionary<string, object?>? tags = null,
        [CallerMemberName] string? memberName = null)
    {
        return new ResultMetricsScope<T>(operationName, tags, memberName);
    }
    
    #endregion
    
    #region Performance Metrics
    
    /// <summary>
    /// Record performance metrics with detailed timing breakdown
    /// </summary>
    public static Result<T> RecordPerformanceMetrics<T>(
        this Result<T> result,
        string operationName,
        TimeSpan executionTime,
        TimeSpan? validationTime = null,
        TimeSpan? businessRuleTime = null,
        TimeSpan? persistenceTime = null,
        [CallerMemberName] string? memberName = null)
    {
        var tags = CreateBaseTags(operationName, memberName, typeof(T).Name);
        
        // Record total execution time
        OperationDuration.Record(executionTime.TotalMilliseconds, tags);
        
        // Record component timing if provided
        if (validationTime.HasValue)
        {
            var validationTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("component", "validation"),
                new("caller.member", memberName ?? "Unknown")
            };
            Meter.CreateHistogram<double>("axon.result.component.duration", "milliseconds")
                .Record(validationTime.Value.TotalMilliseconds, validationTags);
        }
        
        if (businessRuleTime.HasValue)
        {
            var businessRuleTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("component", "business_rules"),
                new("caller.member", memberName ?? "Unknown")
            };
            Meter.CreateHistogram<double>("axon.result.component.duration", "milliseconds")
                .Record(businessRuleTime.Value.TotalMilliseconds, businessRuleTags);
        }
        
        if (persistenceTime.HasValue)
        {
            var persistenceTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("component", "persistence"),
                new("caller.member", memberName ?? "Unknown")
            };
            Meter.CreateHistogram<double>("axon.result.component.duration", "milliseconds")
                .Record(persistenceTime.Value.TotalMilliseconds, persistenceTags);
        }
        
        return result.RecordMetrics(operationName, executionTime, memberName: memberName);
    }
    
    #endregion
    
    #region Business Metrics
    
    /// <summary>
    /// Record business-specific metrics based on Result outcome
    /// </summary>
    public static Result<T> RecordBusinessMetrics<T>(
        this Result<T> result,
        string businessDomain,
        string businessOperation,
        decimal? businessValue = null,
        string? businessUnit = null,
        [CallerMemberName] string? memberName = null)
    {
        var businessTags = new KeyValuePair<string, object?>[]
        {
            new("business.domain", businessDomain),
            new("business.operation", businessOperation),
            new("business.unit", businessUnit ?? "unknown"),
            new("result.is_success", result.IsSuccess),
            new("caller.member", memberName ?? "Unknown")
        };
        
        // Business operation counter
        Meter.CreateCounter<long>("axon.business.operations.count", "operations")
            .Add(1, businessTags);
        
        // Business value tracking
        if (businessValue.HasValue && result.IsSuccess)
        {
            var valueTags = new KeyValuePair<string, object?>[]
            {
                new("business.domain", businessDomain),
                new("business.operation", businessOperation),
                new("business.unit", businessUnit ?? "unknown")
            };
            
            Meter.CreateHistogram<double>("axon.business.value", "units")
                .Record((double)businessValue.Value, valueTags);
        }
        
        // Business error tracking
        if (result.IsFailure)
        {
            var errorTags = new KeyValuePair<string, object?>[]
            {
                new("business.domain", businessDomain),
                new("business.operation", businessOperation),
                new("error.code", result.Error.Code),
                new("error.type", result.Error.Type.ToString())
            };
            
            Meter.CreateCounter<long>("axon.business.errors.count", "errors")
                .Add(1, errorTags);
        }
        
        return result;
    }
    
    #endregion
    
    #region Retry and Resilience Metrics
    
    /// <summary>
    /// Record retry metrics for resilience patterns
    /// </summary>
    public static Result<T> RecordRetryMetrics<T>(
        this Result<T> result,
        string operationName,
        int attemptNumber,
        TimeSpan totalRetryTime,
        bool isLastAttempt,
        [CallerMemberName] string? memberName = null)
    {
        var retryTags = new KeyValuePair<string, object?>[]
        {
            new("operation.name", operationName),
            new("retry.attempt", attemptNumber),
            new("retry.is_last_attempt", isLastAttempt),
            new("retry.result", result.IsSuccess ? "success" : "failure"),
            new("caller.member", memberName ?? "Unknown")
        };
        
        RetryAttempts.Record(attemptNumber, retryTags);
        
        Meter.CreateHistogram<double>("axon.result.retry.total_time", "milliseconds")
            .Record(totalRetryTime.TotalMilliseconds, retryTags);
        
        if (result.IsFailure && isLastAttempt)
        {
            Meter.CreateCounter<long>("axon.result.retry.exhausted.count", "operations")
                .Add(1, retryTags);
        }
        
        return result;
    }
    
    #endregion
    
    #region Batch and Collection Metrics
    
    /// <summary>
    /// Record metrics for batch Result operations
    /// </summary>
    public static IEnumerable<Result<T>> RecordBatchMetrics<T>(
        this IEnumerable<Result<T>> results,
        string batchName,
        TimeSpan? totalProcessingTime = null,
        [CallerMemberName] string? memberName = null)
    {
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.IsSuccess);
        var failureCount = resultList.Count - successCount;
        var successRate = resultList.Count > 0 ? (double)successCount / resultList.Count : 0.0;
        
        var batchTags = new KeyValuePair<string, object?>[]
        {
            new("batch.name", batchName),
            new("batch.size", resultList.Count),
            new("batch.success_count", successCount),
            new("batch.failure_count", failureCount),
            new("batch.success_rate", successRate),
            new("caller.member", memberName ?? "Unknown"),
            new("result.type", typeof(T).Name)
        };
        
        // Batch processing metrics
        Meter.CreateCounter<long>("axon.result.batch.processed.count", "batches")
            .Add(1, batchTags);
            
        Meter.CreateHistogram<long>("axon.result.batch.size", "items")
            .Record(resultList.Count, batchTags);
            
        Meter.CreateGauge<double>("axon.result.batch.success_rate", "percentage")
            .Record(successRate * 100, batchTags);
        
        if (totalProcessingTime.HasValue)
        {
            Meter.CreateHistogram<double>("axon.result.batch.processing_time", "milliseconds")
                .Record(totalProcessingTime.Value.TotalMilliseconds, batchTags);
                
            // Throughput metric
            var itemsPerSecond = resultList.Count / Math.Max(totalProcessingTime.Value.TotalSeconds, 0.001);
            Meter.CreateGauge<double>("axon.result.batch.throughput", "items_per_second")
                .Record(itemsPerSecond, batchTags);
        }
        
        // Record error breakdown
        if (failureCount > 0)
        {
            var errorGroups = resultList
                .Where(r => r.IsFailure)
                .GroupBy(r => r.Error.Code)
                .ToList();
                
            foreach (var group in errorGroups)
            {
                var errorTags = new KeyValuePair<string, object?>[]
                {
                    new("batch.name", batchName),
                    new("error.code", group.Key),
                    new("error.count", group.Count())
                };
                
                Meter.CreateCounter<long>("axon.result.batch.error_breakdown.count", "errors")
                    .Add(group.Count(), errorTags);
            }
        }
        
        return resultList;
    }
    
    #endregion
    
    #region Resource Usage Metrics
    
    /// <summary>
    /// Record resource usage metrics for Result operations
    /// </summary>
    public static Result<T> RecordResourceMetrics<T>(
        this Result<T> result,
        string operationName,
        long memoryAllocated = 0,
        long memoryFreed = 0,
        int threadPoolThreadsUsed = 0,
        int databaseConnections = 0,
        [CallerMemberName] string? memberName = null)
    {
        var resourceTags = new KeyValuePair<string, object?>[]
        {
            new("operation.name", operationName),
            new("result.is_success", result.IsSuccess),
            new("caller.member", memberName ?? "Unknown")
        };
        
        if (memoryAllocated > 0)
        {
            Meter.CreateHistogram<long>("axon.result.memory.allocated", "bytes")
                .Record(memoryAllocated, resourceTags);
        }
        
        if (memoryFreed > 0)
        {
            Meter.CreateHistogram<long>("axon.result.memory.freed", "bytes")
                .Record(memoryFreed, resourceTags);
        }
        
        if (threadPoolThreadsUsed > 0)
        {
            Meter.CreateHistogram<int>("axon.result.threads.used", "threads")
                .Record(threadPoolThreadsUsed, resourceTags);
        }
        
        if (databaseConnections > 0)
        {
            Meter.CreateHistogram<int>("axon.result.database.connections", "connections")
                .Record(databaseConnections, resourceTags);
        }
        
        return result;
    }
    
    #endregion
    
    #region Helper Methods
    
    private static KeyValuePair<string, object?>[] CreateBaseTags(
        string operationName,
        string? memberName,
        string resultType)
    {
        return new KeyValuePair<string, object?>[]
        {
            new("operation.name", operationName),
            new("caller.member", memberName ?? "Unknown"),
            new("result.type", resultType)
        };
    }
    
    #endregion
}

/// <summary>
/// Disposable metrics scope for Result operations with automatic metric recording
/// </summary>
public sealed class ResultMetricsScope<T> : IDisposable
{
    private readonly string _operationName;
    private readonly IDictionary<string, object?>? _tags;
    private readonly string _memberName;
    private readonly Stopwatch _stopwatch;
    private readonly long _startActiveOperations;
    private bool _disposed;
    
    internal ResultMetricsScope(
        string operationName,
        IDictionary<string, object?>? tags,
        string? memberName)
    {
        _operationName = operationName;
        _tags = tags;
        _memberName = memberName ?? "Unknown";
        _stopwatch = Stopwatch.StartNew();
        
        // Track active operations
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("operation.name", operationName),
            new("caller.member", _memberName)
        };
        
        ResultMetricsExtensions.ActiveOperations.Add(1, baseTags);
        _startActiveOperations = Environment.TickCount64;
    }
    
    /// <summary>
    /// Complete the operation with a Result and record all metrics
    /// </summary>
    public Result<T> Complete(Result<T> result)
    {
        if (_disposed) return result;
        
        _stopwatch.Stop();
        
        // Record metrics
        result.RecordMetrics(_operationName, _stopwatch.Elapsed, _tags, _memberName);
        
        // Update active operations
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("operation.name", _operationName),
            new("caller.member", _memberName),
            new("result.is_success", result.IsSuccess)
        };
        
        ResultMetricsExtensions.ActiveOperations.Add(-1, baseTags);
        
        return result;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _stopwatch.Stop();
        
        // Handle incomplete operation
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("operation.name", _operationName),
            new("caller.member", _memberName),
            new("result.is_success", false),
            new("operation.status", "incomplete")
        };
        
        ResultMetricsExtensions.ActiveOperations.Add(-1, baseTags);
        
        // Record incomplete operation metric
        var incompleteCounter = ResultMetricsExtensions.Meter.CreateCounter<long>(
            "axon.result.incomplete.count", "operations");
        incompleteCounter.Add(1, baseTags);
        
        _disposed = true;
    }
}