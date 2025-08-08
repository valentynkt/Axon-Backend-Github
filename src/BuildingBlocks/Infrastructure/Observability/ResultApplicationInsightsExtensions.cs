using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Application Insights integration extensions for Result&lt;T&gt;.
/// Provides telemetry tracking, custom metrics, dependency tracking,
/// and intelligent sampling for Result-based operations.
/// </summary>
public static class ResultApplicationInsightsExtensions
{
    #region Telemetry Tracking Extensions
    
    /// <summary>
    /// Track Result operation with comprehensive Application Insights telemetry
    /// </summary>
    public static Result<T> TrackResultOperation<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string operationName,
        TimeSpan? duration = null,
        IDictionary<string, string>? properties = null,
        IDictionary<string, double>? metrics = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var telemetryProperties = CreateBaseTelemetryProperties(operationName, memberName, typeof(T).Name);
        
        // Add custom properties
        if (properties != null)
        {
            foreach (var (key, value) in properties)
            {
                telemetryProperties[key] = value;
            }
        }
        
        var telemetryMetrics = new Dictionary<string, double>();
        if (metrics != null)
        {
            foreach (var (key, value) in metrics)
            {
                telemetryMetrics[key] = value;
            }
        }
        
        if (duration.HasValue)
        {
            telemetryMetrics["durationMs"] = duration.Value.TotalMilliseconds;
        }
        
        if (result.IsSuccess)
        {
            telemetryProperties["resultState"] = "Success";
            telemetryClient.TrackEvent($"Result.{operationName}.Success", telemetryProperties, telemetryMetrics);
        }
        else
        {
            var error = result.Error;
            telemetryProperties["resultState"] = "Failure";
            telemetryProperties["errorCode"] = error.Code;
            telemetryProperties["errorType"] = error.Type.ToString();
            telemetryProperties["errorSeverity"] = error.Severity.ToString();
            telemetryProperties["httpStatusCode"] = error.ToHttpStatusCode().ToString();
            
            if (!string.IsNullOrEmpty(error.CorrelationId))
            {
                telemetryProperties["correlationId"] = error.CorrelationId;
            }
            
            if (!string.IsNullOrEmpty(error.Source))
            {
                telemetryProperties["errorSource"] = error.Source;
            }
            
            // Add error metadata
            if (error.Metadata != null)
            {
                foreach (var (key, value) in error.Metadata.Take(10)) // Limit metadata
                {
                    telemetryProperties[$"errorMetadata.{key}"] = value?.ToString() ?? "null";
                }
            }
            
            telemetryClient.TrackEvent($"Result.{operationName}.Failure", telemetryProperties, telemetryMetrics);
            
            // Track as exception if there's an inner exception
            if (error.InnerException != null)
            {
                var exceptionTelemetry = new ExceptionTelemetry(error.InnerException)
                {
                    Message = error.Message,
                    SeverityLevel = GetSeverityLevel(error.Severity)
                };
                
                foreach (var (key, value) in telemetryProperties)
                {
                    exceptionTelemetry.Properties[key] = value;
                }
                
                foreach (var (key, value) in telemetryMetrics)
                {
                    exceptionTelemetry.Metrics[key] = value;
                }
                
                telemetryClient.TrackException(exceptionTelemetry);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Track Result as custom metric with success rate and performance data
    /// </summary>
    public static Result<T> TrackResultMetrics<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string metricName,
        double? value = null,
        IDictionary<string, string>? dimensions = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var telemetryDimensions = new Dictionary<string, string>
        {
            ["operation"] = metricName,
            ["resultType"] = typeof(T).Name,
            ["callerMember"] = memberName ?? "Unknown",
            ["isSuccess"] = result.IsSuccess.ToString()
        };
        
        if (dimensions != null)
        {
            foreach (var (key, dimensionValue) in dimensions)
            {
                telemetryDimensions[key] = dimensionValue;
            }
        }
        
        if (result.IsFailure)
        {
            var error = result.Error;
            telemetryDimensions["errorCode"] = error.Code;
            telemetryDimensions["errorType"] = error.Type.ToString();
            telemetryDimensions["errorSeverity"] = error.Severity.ToString();
        }
        
        // Track success rate metric
        telemetryClient.GetMetric($"{metricName}.SuccessRate", telemetryDimensions.Keys.ToArray())
            .TrackValue(result.IsSuccess ? 1.0 : 0.0, telemetryDimensions.Values.ToArray());
        
        // Track custom value if provided
        if (value.HasValue)
        {
            telemetryClient.GetMetric($"{metricName}.Value", telemetryDimensions.Keys.ToArray())
                .TrackValue(value.Value, telemetryDimensions.Values.ToArray());
        }
        
        return result;
    }
    
    #endregion
    
    #region Dependency Tracking
    
    /// <summary>
    /// Track Result operation as dependency call
    /// </summary>
    public static Result<T> TrackResultDependency<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string dependencyTypeName,
        string dependencyName,
        string commandName,
        DateTimeOffset startTime,
        TimeSpan duration,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var dependencyTelemetry = new DependencyTelemetry(
            dependencyTypeName,
            dependencyName,
            commandName,
            startTime,
            duration,
            result.IsSuccess);
        
        dependencyTelemetry.Properties["resultType"] = typeof(T).Name;
        dependencyTelemetry.Properties["callerMember"] = memberName ?? "Unknown";
        
        if (result.IsFailure)
        {
            var error = result.Error;
            dependencyTelemetry.Properties["errorCode"] = error.Code;
            dependencyTelemetry.Properties["errorType"] = error.Type.ToString();
            dependencyTelemetry.Properties["errorMessage"] = error.Message;
            dependencyTelemetry.ResultCode = error.ToHttpStatusCode().ToString();
            
            if (!string.IsNullOrEmpty(error.CorrelationId))
            {
                dependencyTelemetry.Properties["correlationId"] = error.CorrelationId;
            }
        }
        else
        {
            dependencyTelemetry.ResultCode = "200";
        }
        
        telemetryClient.TrackDependency(dependencyTelemetry);
        
        return result;
    }
    
    /// <summary>
    /// Create dependency tracking scope for Result operations
    /// </summary>
    public static ResultDependencyScope<T> StartDependencyTracking<T>(
        this TelemetryClient telemetryClient,
        string dependencyTypeName,
        string dependencyName,
        string commandName)
    {
        return new ResultDependencyScope<T>(telemetryClient, dependencyTypeName, dependencyName, commandName);
    }
    
    #endregion
    
    #region Request Tracking
    
    /// <summary>
    /// Track Result as request telemetry
    /// </summary>
    public static Result<T> TrackResultRequest<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string requestName,
        DateTimeOffset startTime,
        TimeSpan duration,
        string? responseCode = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var actualResponseCode = responseCode ?? (result.IsSuccess ? "200" : result.Error.ToHttpStatusCode().ToString());
        
        var requestTelemetry = new RequestTelemetry(
            requestName,
            startTime,
            duration,
            actualResponseCode,
            result.IsSuccess);
        
        requestTelemetry.Properties["resultType"] = typeof(T).Name;
        requestTelemetry.Properties["callerMember"] = memberName ?? "Unknown";
        
        if (result.IsFailure)
        {
            var error = result.Error;
            requestTelemetry.Properties["errorCode"] = error.Code;
            requestTelemetry.Properties["errorType"] = error.Type.ToString();
            requestTelemetry.Properties["errorMessage"] = error.Message;
            
            if (!string.IsNullOrEmpty(error.CorrelationId))
            {
                requestTelemetry.Properties["correlationId"] = error.CorrelationId;
            }
        }
        
        telemetryClient.TrackRequest(requestTelemetry);
        
        return result;
    }
    
    #endregion
    
    #region Custom Events and Traces
    
    /// <summary>
    /// Track Result business event with custom properties
    /// </summary>
    public static Result<T> TrackBusinessEvent<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string eventName,
        object? eventData = null,
        [CallerMemberName] string? memberName = null)
    {
        if (!result.IsSuccess) return result;
        
        var properties = CreateBaseTelemetryProperties(eventName, memberName, typeof(T).Name);
        properties["eventType"] = "Business";
        
        var metrics = new Dictionary<string, double>();
        
        // Add event data properties
        if (eventData != null)
        {
            var eventProperties = eventData.GetType().GetProperties();
            foreach (var prop in eventProperties.Take(20)) // Limit properties
            {
                try
                {
                    var value = prop.GetValue(eventData);
                    if (value != null)
                    {
                        if (IsNumericType(prop.PropertyType))
                        {
                            if (double.TryParse(value.ToString(), out var numericValue))
                            {
                                metrics[prop.Name] = numericValue;
                            }
                        }
                        else
                        {
                            properties[prop.Name] = value.ToString() ?? "null";
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore property access errors
                }
            }
        }
        
        telemetryClient.TrackEvent($"Business.{eventName}", properties, metrics);
        
        return result;
    }
    
    /// <summary>
    /// Track Result with custom trace information
    /// </summary>
    public static Result<T> TrackTrace<T>(
        this TelemetryClient telemetryClient,
        Result<T> result,
        string message,
        SeverityLevel severityLevel = SeverityLevel.Information,
        IDictionary<string, string>? properties = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var telemetryProperties = CreateBaseTelemetryProperties(message, memberName, typeof(T).Name);
        telemetryProperties["resultState"] = result.IsSuccess ? "Success" : "Failure";
        
        if (properties != null)
        {
            foreach (var (key, value) in properties)
            {
                telemetryProperties[key] = value;
            }
        }
        
        if (result.IsFailure)
        {
            var error = result.Error;
            telemetryProperties["errorCode"] = error.Code;
            telemetryProperties["errorMessage"] = error.Message;
            severityLevel = GetSeverityLevel(error.Severity);
        }
        
        var traceTelemetry = new TraceTelemetry(message, severityLevel);
        foreach (var (key, value) in telemetryProperties)
        {
            traceTelemetry.Properties[key] = value;
        }
        
        telemetryClient.TrackTrace(traceTelemetry);
        
        return result;
    }
    
    #endregion
    
    #region Batch Operations
    
    /// <summary>
    /// Track batch Result operations with aggregated telemetry
    /// </summary>
    public static IEnumerable<Result<T>> TrackBatchResults<T>(
        this TelemetryClient telemetryClient,
        IEnumerable<Result<T>> results,
        string batchName,
        TimeSpan? totalDuration = null,
        [CallerMemberName] string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(telemetryClient);
        
        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.IsSuccess);
        var failureCount = resultList.Count - successCount;
        var successRate = resultList.Count > 0 ? (double)successCount / resultList.Count : 0.0;
        
        var properties = new Dictionary<string, string>
        {
            ["batchName"] = batchName,
            ["callerMember"] = memberName ?? "Unknown",
            ["resultType"] = typeof(T).Name,
            ["totalCount"] = resultList.Count.ToString(),
            ["successCount"] = successCount.ToString(),
            ["failureCount"] = failureCount.ToString()
        };
        
        var metrics = new Dictionary<string, double>
        {
            ["batchSize"] = resultList.Count,
            ["successRate"] = successRate * 100, // As percentage
            ["failureRate"] = (1.0 - successRate) * 100
        };
        
        if (totalDuration.HasValue)
        {
            metrics["totalDurationMs"] = totalDuration.Value.TotalMilliseconds;
            metrics["averageItemDurationMs"] = totalDuration.Value.TotalMilliseconds / Math.Max(resultList.Count, 1);
        }
        
        telemetryClient.TrackEvent($"Batch.{batchName}", properties, metrics);
        
        // Track error breakdown if there are failures
        if (failureCount > 0)
        {
            var errorGroups = resultList
                .Where(r => r.IsFailure)
                .GroupBy(r => r.Error.Code)
                .ToList();
                
            foreach (var group in errorGroups)
            {
                var errorProperties = new Dictionary<string, string>
                {
                    ["batchName"] = batchName,
                    ["errorCode"] = group.Key,
                    ["errorCount"] = group.Count().ToString()
                };
                
                var errorMetrics = new Dictionary<string, double>
                {
                    ["errorCount"] = group.Count(),
                    ["errorPercentage"] = (double)group.Count() / resultList.Count * 100
                };
                
                telemetryClient.TrackEvent($"BatchError.{batchName}", errorProperties, errorMetrics);
            }
        }
        
        return resultList;
    }
    
    #endregion
    
    #region Helper Methods
    
    private static Dictionary<string, string> CreateBaseTelemetryProperties(
        string operationName,
        string? memberName,
        string resultType)
    {
        return new Dictionary<string, string>
        {
            ["operation"] = operationName,
            ["callerMember"] = memberName ?? "Unknown",
            ["resultType"] = resultType,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };
    }
    
    private static SeverityLevel GetSeverityLevel(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => SeverityLevel.Information,
        ErrorSeverity.Warning => SeverityLevel.Warning,
        ErrorSeverity.Error => SeverityLevel.Error,
        ErrorSeverity.Critical => SeverityLevel.Critical,
        ErrorSeverity.Fatal => SeverityLevel.Critical,
        _ => SeverityLevel.Error
    };
    
    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
               type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
               type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
               type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(int?) || type == typeof(long?) || type == typeof(float?) ||
               type == typeof(double?) || type == typeof(decimal?) || type == typeof(short?) ||
               type == typeof(uint?) || type == typeof(ulong?) || type == typeof(ushort?) ||
               type == typeof(byte?) || type == typeof(sbyte?);
    }
    
    #endregion
}

/// <summary>
/// Disposable scope for tracking Result dependency operations
/// </summary>
public sealed class ResultDependencyScope<T> : IDisposable
{
    private readonly TelemetryClient _telemetryClient;
    private readonly string _dependencyTypeName;
    private readonly string _dependencyName;
    private readonly string _commandName;
    private readonly DateTimeOffset _startTime;
    private bool _disposed;
    
    internal ResultDependencyScope(
        TelemetryClient telemetryClient,
        string dependencyTypeName,
        string dependencyName,
        string commandName)
    {
        _telemetryClient = telemetryClient;
        _dependencyTypeName = dependencyTypeName;
        _dependencyName = dependencyName;
        _commandName = commandName;
        _startTime = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Complete the dependency operation with a Result
    /// </summary>
    public Result<T> Complete(Result<T> result)
    {
        if (_disposed) return result;
        
        var duration = DateTimeOffset.UtcNow - _startTime;
        return _telemetryClient.TrackResultDependency(
            result,
            _dependencyTypeName,
            _dependencyName,
            _commandName,
            _startTime,
            duration);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        var duration = DateTimeOffset.UtcNow - _startTime;
        var failureResult = Result<T>.Failure(Error.Internal(
            "Dependency operation was not completed properly",
            "DEPENDENCY_INCOMPLETE"));
            
        _telemetryClient.TrackResultDependency(
            failureResult,
            _dependencyTypeName,
            _dependencyName,
            _commandName,
            _startTime,
            duration);
            
        _disposed = true;
    }
}