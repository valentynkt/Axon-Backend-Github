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

    // --- Added: centralize instruments that were previously created inside methods ---

    // Component timing
    private static readonly Histogram<double> ComponentDuration = Meter.CreateHistogram<double>(
        "axon.result.component.duration", "milliseconds", "Duration by component");

    // Retry metrics
    private static readonly Histogram<double> RetryTotalTime = Meter.CreateHistogram<double>(
        "axon.result.retry.total_time", "milliseconds", "Total retry time for operations");

    private static readonly Counter<long> RetryExhaustedCount = Meter.CreateCounter<long>(
        "axon.result.retry.exhausted.count", "operations", "Operations that exhausted retries");

    // Business metrics
    private static readonly Counter<long> BusinessOperationsCount = Meter.CreateCounter<long>(
        "axon.business.operations.count", "operations", "Business operation count");

    private static readonly Histogram<double> BusinessValue = Meter.CreateHistogram<double>(
        "axon.business.value", "units", "Business value recorded on success");

    private static readonly Counter<long> BusinessErrorsCount = Meter.CreateCounter<long>(
        "axon.business.errors.count", "errors", "Business errors");

    // Batch metrics
    private static readonly Counter<long> BatchProcessedCount = Meter.CreateCounter<long>(
        "axon.result.batch.processed.count", "batches", "Batches processed");

    private static readonly Histogram<long> BatchSize = Meter.CreateHistogram<long>(
        "axon.result.batch.size", "items", "Batch size");

    private static readonly Gauge<double> BatchSuccessRate = Meter.CreateGauge<double>(
        "axon.result.batch.success_rate", "percentage", "Batch success rate");

    private static readonly Histogram<double> BatchProcessingTime = Meter.CreateHistogram<double>(
        "axon.result.batch.processing_time", "milliseconds", "Batch processing time");

    private static readonly Gauge<double> BatchThroughput = Meter.CreateGauge<double>(
        "axon.result.batch.throughput", "items_per_second", "Batch throughput");

    private static readonly Counter<long> BatchErrorBreakdownCount = Meter.CreateCounter<long>(
        "axon.result.batch.error_breakdown.count", "errors", "Batch error breakdown by code");

    // Resource metrics
    private static readonly Histogram<long> MemoryAllocated = Meter.CreateHistogram<long>(
        "axon.result.memory.allocated", "bytes", "Memory allocated during operation");

    private static readonly Histogram<long> MemoryFreed = Meter.CreateHistogram<long>(
        "axon.result.memory.freed", "bytes", "Memory freed during operation");

    private static readonly Histogram<int> ThreadsUsed = Meter.CreateHistogram<int>(
        "axon.result.threads.used", "threads", "ThreadPool threads used");

    private static readonly Histogram<int> DatabaseConnections = Meter.CreateHistogram<int>(
        "axon.result.database.connections", "connections", "Database connections used");

    // Incomplete ops
    private static readonly Counter<long> IncompleteCounter = Meter.CreateCounter<long>(
        "axon.result.incomplete.count", "operations", "Operations disposed without completion");

    // Internal bridge so non-nested types can update ActiveOperations safely
    internal static void AddActiveOperations(long delta, KeyValuePair<string, object?>[] tags) =>
        ActiveOperations.Add(delta, tags);
    
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
            ComponentDuration.Record(validationTime.Value.TotalMilliseconds, validationTags);
        }
        
        if (businessRuleTime.HasValue)
        {
            var businessRuleTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("component", "business_rules"),
                new("caller.member", memberName ?? "Unknown")
            };
            ComponentDuration.Record(businessRuleTime.Value.TotalMilliseconds, businessRuleTags);
        }
        
        if (persistenceTime.HasValue)
        {
            var persistenceTags = new KeyValuePair<string, object?>[]
            {
                new("operation.name", operationName),
                new("component", "persistence"),
                new("caller.member", memberName ?? "Unknown")
            };
            ComponentDuration.Record(persistenceTime.Value.TotalMilliseconds, persistenceTags);
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
        BusinessOperationsCount.Add(1, businessTags);
        
        // Business value tracking
        if (businessValue.HasValue && result.IsSuccess)
        {
            var valueTags = new KeyValuePair<string, object?>[]
            {
                new("business.domain", businessDomain),
                new("business.operation", businessOperation),
                new("business.unit", businessUnit ?? "unknown")
            };
            
            ResultMetricsExtensions.BusinessValue.Record((double)businessValue.Value, valueTags);
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
            
            BusinessErrorsCount.Add(1, errorTags);
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
        RetryTotalTime.Record(totalRetryTime.TotalMilliseconds, retryTags);
        
        if (result.IsFailure && isLastAttempt)
        {
            RetryExhaustedCount.Add(1, retryTags);
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
        BatchProcessedCount.Add(1, batchTags);
        BatchSize.Record(resultList.Count, batchTags);
        BatchSuccessRate.Record(successRate * 100, batchTags);
        
        if (totalProcessingTime.HasValue)
        {
            BatchProcessingTime.Record(totalProcessingTime.Value.TotalMilliseconds, batchTags);
                
            // Throughput metric
            var itemsPerSecond = resultList.Count / Math.Max(totalProcessingTime.Value.TotalSeconds, 0.001);
            BatchThroughput.Record(itemsPerSecond, batchTags);
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
                
                BatchErrorBreakdownCount.Add(group.Count(), errorTags);
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
            MemoryAllocated.Record(memoryAllocated, resourceTags);
        }
        
        if (memoryFreed > 0)
        {
            MemoryFreed.Record(memoryFreed, resourceTags);
        }
        
        if (threadPoolThreadsUsed > 0)
        {
            ThreadsUsed.Record(threadPoolThreadsUsed, resourceTags);
        }
        
        if (databaseConnections > 0)
        {
            DatabaseConnections.Record(databaseConnections, resourceTags);
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
        
        ResultMetricsExtensions.AddActiveOperations(1, baseTags);
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
        
        ResultMetricsExtensions.AddActiveOperations(-1, baseTags);
        
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
        
        ResultMetricsExtensions.AddActiveOperations(-1, baseTags);
        
        // Record incomplete operation metric
        // (Use centralized instrument instead of creating a new one per call)
        System.Diagnostics.Metrics.Counter<long> incompleteCounterField =
            (System.Diagnostics.Metrics.Counter<long>)
            typeof(ResultMetricsExtensions)
                .GetField("IncompleteCounter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;
        incompleteCounterField.Add(1, baseTags);
        
        _disposed = true;
    }
}
