using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Comprehensive Result&lt;T&gt; observability extensions for structured logging,
/// performance metrics, and diagnostic information.
/// Provides zero-allocation logging paths and integrated telemetry support.
/// </summary>
public static class ResultLoggingExtensions
{
    #region Structured Logging Extensions
    
    /// <summary>
    /// Log Result success with structured data and performance metrics
    /// </summary>
    public static Result<T> LogSuccess<T>(
        this Result<T> result,
        ILogger logger,
        string message = "Operation completed successfully",
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (result.IsSuccess && logger.IsEnabled(LogLevel.Information))
        {
            using var scope = logger.BeginScope(CreateSuccessScope(result, memberName, sourceFilePath, sourceLineNumber));
            logger.LogInformation("{Message} | Result: Success | Type: {ResultType}", 
                message, typeof(T).Name);
        }
        
        return result;
    }
    
    /// <summary>
    /// Log Result failure with comprehensive error details
    /// </summary>
    public static Result<T> LogFailure<T>(
        this Result<T> result,
        ILogger logger,
        string message = "Operation failed",
        LogLevel logLevel = LogLevel.Error,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (result.IsFailure && logger.IsEnabled(logLevel))
        {
            using var scope = logger.BeginScope(CreateFailureScope(result, memberName, sourceFilePath, sourceLineNumber));
            
            var error = result.Error;
            logger.Log(logLevel, 
                "{Message} | Result: Failure | ErrorCode: {ErrorCode} | ErrorType: {ErrorType} | ErrorMessage: {ErrorMessage}",
                message, error.Code, error.Type, error.Message);
                
            // Log detailed error metadata if available
            if (error.Metadata != null && error.Metadata.Count > 0)
            {
                logger.LogDebug("Error metadata: {@ErrorMetadata}", error.Metadata);
            }
            
            // Log inner exception if present
            if (error.InnerException != null)
            {
                logger.LogDebug(error.InnerException, "Inner exception details");
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Log Result with automatic success/failure handling
    /// </summary>
    public static Result<T> LogResult<T>(
        this Result<T> result,
        ILogger logger,
        string? successMessage = null,
        string? failureMessage = null,
        LogLevel successLevel = LogLevel.Information,
        LogLevel failureLevel = LogLevel.Error,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        return result.IsSuccess
            ? result.LogSuccess(logger, successMessage ?? "Operation completed successfully", memberName, sourceFilePath, sourceLineNumber)
            : result.LogFailure(logger, failureMessage ?? "Operation failed", failureLevel, memberName, sourceFilePath, sourceLineNumber);
    }
    
    #endregion
    
    #region Performance Logging Extensions
    
    /// <summary>
    /// Log Result with execution timing and performance metrics
    /// </summary>
    public static Result<T> LogWithTiming<T>(
        this Result<T> result,
        ILogger logger,
        Stopwatch stopwatch,
        string operationName,
        TimeSpan? slowThreshold = null,
        [CallerMemberName] string? memberName = null)
    {
        var elapsed = stopwatch.Elapsed;
        var threshold = slowThreshold ?? TimeSpan.FromMilliseconds(1000);
        
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationName"] = operationName,
            ["ElapsedMs"] = elapsed.TotalMilliseconds,
            ["ResultType"] = typeof(T).Name,
            ["IsSuccess"] = result.IsSuccess,
            ["CallerMember"] = memberName ?? "Unknown",
            ["IsSlowOperation"] = elapsed > threshold
        });
        
        if (result.IsSuccess)
        {
            var logLevel = elapsed > threshold ? LogLevel.Warning : LogLevel.Information;
            logger.Log(logLevel, 
                "{OperationName} completed successfully in {ElapsedMs}ms{SlowIndicator}",
                operationName, elapsed.TotalMilliseconds, 
                elapsed > threshold ? " (SLOW)" : "");
        }
        else
        {
            var error = result.Error;
            logger.LogError(
                "{OperationName} failed in {ElapsedMs}ms | ErrorCode: {ErrorCode} | ErrorMessage: {ErrorMessage}",
                operationName, elapsed.TotalMilliseconds, error.Code, error.Message);
        }
        
        return result;
    }
    
    /// <summary>
    /// Create a timing scope for Result operations
    /// </summary>
    public static TimedResultScope<T> TimeOperation<T>(
        this ILogger logger,
        string operationName,
        LogLevel successLevel = LogLevel.Information,
        LogLevel failureLevel = LogLevel.Error,
        TimeSpan? slowThreshold = null)
    {
        return new TimedResultScope<T>(logger, operationName, successLevel, failureLevel, slowThreshold);
    }
    
    #endregion
    
    #region Conditional Logging Extensions
    
    /// <summary>
    /// Log Result only if condition is met
    /// </summary>
    public static Result<T> LogIf<T>(
        this Result<T> result,
        ILogger logger,
        Func<Result<T>, bool> condition,
        string message,
        LogLevel logLevel = LogLevel.Information)
    {
        if (condition(result) && logger.IsEnabled(logLevel))
        {
            if (result.IsSuccess)
            {
                logger.Log(logLevel, "{Message} | Result: Success", message);
            }
            else
            {
                logger.Log(logLevel, "{Message} | Result: Failure | Error: {ErrorMessage}", 
                    message, result.Error.Message);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Log Result only on failure
    /// </summary>
    public static Result<T> LogFailureOnly<T>(
        this Result<T> result,
        ILogger logger,
        string message = "Operation failed",
        LogLevel logLevel = LogLevel.Error)
    {
        return result.LogIf(logger, r => r.IsFailure, message, logLevel);
    }
    
    /// <summary>
    /// Log Result only on success
    /// </summary>
    public static Result<T> LogSuccessOnly<T>(
        this Result<T> result,
        ILogger logger,
        string message = "Operation completed successfully",
        LogLevel logLevel = LogLevel.Information)
    {
        return result.LogIf(logger, r => r.IsSuccess, message, logLevel);
    }
    
    #endregion
    
    #region Advanced Logging Extensions
    
    /// <summary>
    /// Log Result with custom scope data
    /// </summary>
    public static Result<T> LogWithScope<T>(
        this Result<T> result,
        ILogger logger,
        IDictionary<string, object> scopeData,
        string message,
        LogLevel logLevel = LogLevel.Information)
    {
        if (logger.IsEnabled(logLevel))
        {
            using var scope = logger.BeginScope(scopeData);
            
            if (result.IsSuccess)
            {
                logger.Log(logLevel, "{Message} | Result: Success", message);
            }
            else
            {
                var error = result.Error;
                logger.Log(logLevel, 
                    "{Message} | Result: Failure | ErrorCode: {ErrorCode} | ErrorMessage: {ErrorMessage}",
                    message, error.Code, error.Message);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Log Result with error severity-based log level
    /// </summary>
    public static Result<T> LogWithSeverity<T>(
        this Result<T> result,
        ILogger logger,
        string message = "Operation completed")
    {
        if (result.IsSuccess)
        {
            logger.LogInformation("{Message} | Result: Success", message);
        }
        else
        {
            var error = result.Error;
            var logLevel = GetLogLevelFromSeverity(error.Severity);
            
            logger.Log(logLevel, 
                "{Message} | Result: Failure | ErrorCode: {ErrorCode} | Severity: {ErrorSeverity} | ErrorMessage: {ErrorMessage}",
                message, error.Code, error.Severity, error.Message);
        }
        
        return result;
    }
    
    /// <summary>
    /// Log aggregated Results with summary statistics
    /// </summary>
    public static IEnumerable<Result<T>> LogBatch<T>(
        this IEnumerable<Result<T>> results,
        ILogger logger,
        string batchName,
        LogLevel logLevel = LogLevel.Information)
    {
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.IsSuccess);
        var failureCount = resultList.Count - successCount;
        
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["BatchName"] = batchName,
            ["TotalCount"] = resultList.Count,
            ["SuccessCount"] = successCount,
            ["FailureCount"] = failureCount,
            ["SuccessRate"] = resultList.Count > 0 ? (double)successCount / resultList.Count : 0.0
        });
        
        logger.Log(logLevel, 
            "Batch '{BatchName}' completed | Total: {TotalCount} | Success: {SuccessCount} | Failures: {FailureCount}",
            batchName, resultList.Count, successCount, failureCount);
            
        // Log failure details if any
        if (failureCount > 0 && logger.IsEnabled(LogLevel.Warning))
        {
            var errorGroups = resultList
                .Where(r => r.IsFailure)
                .GroupBy(r => r.Error.Code)
                .Select(g => new { ErrorCode = g.Key, Count = g.Count() })
                .ToList();
                
            logger.LogWarning("Batch failures breakdown: {@FailureBreakdown}", errorGroups);
        }
        
        return resultList;
    }
    
    #endregion
    
    #region Helper Methods
    
    private static Dictionary<string, object> CreateSuccessScope<T>(
        Result<T> result,
        string? memberName,
        string? sourceFilePath,
        int sourceLineNumber)
    {
        var scope = new Dictionary<string, object>
        {
            ["ResultType"] = typeof(T).Name,
            ["ResultState"] = "Success",
            ["CallerMember"] = memberName ?? "Unknown",
            ["SourceLine"] = sourceLineNumber
        };
        
        if (!string.IsNullOrEmpty(sourceFilePath))
        {
            scope["SourceFile"] = Path.GetFileName(sourceFilePath);
        }
        
        return scope;
    }
    
    private static Dictionary<string, object> CreateFailureScope<T>(
        Result<T> result,
        string? memberName,
        string? sourceFilePath,
        int sourceLineNumber)
    {
        var error = result.Error;
        var scope = new Dictionary<string, object>
        {
            ["ResultType"] = typeof(T).Name,
            ["ResultState"] = "Failure",
            ["ErrorCode"] = error.Code,
            ["ErrorType"] = error.Type.ToString(),
            ["ErrorSeverity"] = error.Severity.ToString(),
            ["HttpStatusCode"] = error.ToHttpStatusCode(),
            ["CallerMember"] = memberName ?? "Unknown",
            ["SourceLine"] = sourceLineNumber
        };
        
        if (!string.IsNullOrEmpty(sourceFilePath))
        {
            scope["SourceFile"] = Path.GetFileName(sourceFilePath);
        }
        
        if (!string.IsNullOrEmpty(error.CorrelationId))
        {
            scope["CorrelationId"] = error.CorrelationId;
        }
        
        if (!string.IsNullOrEmpty(error.Source))
        {
            scope["ErrorSource"] = error.Source;
        }
        
        return scope;
    }
    
    private static LogLevel GetLogLevelFromSeverity(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => LogLevel.Information,
        ErrorSeverity.Warning => LogLevel.Warning,
        ErrorSeverity.Error => LogLevel.Error,
        ErrorSeverity.Critical => LogLevel.Critical,
        ErrorSeverity.Fatal => LogLevel.Critical,
        _ => LogLevel.Error
    };
    
    #endregion
}

/// <summary>
/// Disposable timing scope for Result operations with automatic logging
/// </summary>
public sealed class TimedResultScope<T> : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly LogLevel _successLevel;
    private readonly LogLevel _failureLevel;
    private readonly TimeSpan _slowThreshold;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;
    
    internal TimedResultScope(
        ILogger logger,
        string operationName,
        LogLevel successLevel,
        LogLevel failureLevel,
        TimeSpan? slowThreshold)
    {
        _logger = logger;
        _operationName = operationName;
        _successLevel = successLevel;
        _failureLevel = failureLevel;
        _slowThreshold = slowThreshold ?? TimeSpan.FromMilliseconds(1000);
        _stopwatch = Stopwatch.StartNew();
        
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Starting operation: {OperationName}", operationName);
        }
    }
    
    /// <summary>
    /// Complete the operation with a Result
    /// </summary>
    public Result<T> Complete(Result<T> result)
    {
        if (_disposed) return result;
        
        _stopwatch.Stop();
        return result.LogWithTiming(_logger, _stopwatch, _operationName, _slowThreshold);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _stopwatch.Stop();
        _logger.LogWarning(
            "Operation '{OperationName}' was disposed without completion after {ElapsedMs}ms",
            _operationName, _stopwatch.Elapsed.TotalMilliseconds);
            
        _disposed = true;
    }
}