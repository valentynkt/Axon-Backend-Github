using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Extensions;
using BuildingBlocks.Infrastructure.Observability;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Result-aware logging behavior for MediatR pipeline.
/// Provides structured logging for Result-based operations with performance metrics.
/// Logs success/failure states, timing, and error details without breaking the railway pattern.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type - optimized for Result types</typeparam>
public sealed class ResultLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
{
    private readonly ILogger<ResultLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly TelemetryClient? _telemetryClient;
    private readonly ISensitiveDataMasker _sensitiveDataMasker;

    public ResultLoggingBehavior(
        ILogger<ResultLoggingBehavior<TRequest, TResponse>> logger,
        ISensitiveDataMasker sensitiveDataMasker,
        TelemetryClient? telemetryClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sensitiveDataMasker = sensitiveDataMasker ?? throw new ArgumentNullException(nameof(sensitiveDataMasker));
        _telemetryClient = telemetryClient;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).Name;
        var requestId = request.RequestId;

        // Create masked version of request for logging
        var maskedRequest = _sensitiveDataMasker.MaskSensitiveData(request);
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["RequestId"] = requestId,
            ["RequestedAt"] = request.RequestedAt,
            ["MaskedRequest"] = maskedRequest
        });

        _logger.LogInformation("Starting request {RequestName} with ID {RequestId} - Request: {MaskedRequest}", 
            requestName, requestId, maskedRequest);

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            // Enhanced logging with new observability extensions
            LogResultWithObservability(response, requestName, requestId, stopwatch, startTime);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning("Request {RequestName} with ID {RequestId} was cancelled after {ElapsedMs}ms", 
                requestName, requestId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, 
                "Request {RequestName} with ID {RequestId} failed with exception after {ElapsedMs}ms", 
                requestName, requestId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Enhanced logging with observability extensions for comprehensive telemetry.
    /// </summary>
    private void LogResultWithObservability<T>(
        T response, 
        string requestName, 
        Guid requestId, 
        Stopwatch stopwatch,
        DateTimeOffset startTime)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var duration = stopwatch.Elapsed;
        
        // Base telemetry properties
        var telemetryProperties = new Dictionary<string, string>
        {
            ["RequestName"] = requestName,
            ["RequestId"] = requestId.ToString(),
            ["RequestType"] = typeof(T).Name,
            ["StartTime"] = startTime.ToString("O"),
            ["CompletedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        
        var telemetryMetrics = new Dictionary<string, double>
        {
            ["DurationMs"] = elapsedMs,
            ["DurationSeconds"] = duration.TotalSeconds
        };

        // Handle Result<T> response types with enhanced observability
        if (response is IResult result)
        {
            // Create a typed Result for logging extensions
            if (TryCreateTypedResult(response, out var typedResult))
            {
                // Use the new logging extensions
                typedResult
                    .LogResult(_logger, 
                        $"Request {requestName} completed", 
                        $"Request {requestName} failed")
                    .EnrichActivity(requestName)
                    .RecordMetrics(requestName, duration)
                    .WithActivityCorrelationId();

                // Application Insights telemetry
                if (_telemetryClient != null)
                {
                    _telemetryClient.TrackResultOperation(
                        typedResult,
                        requestName,
                        duration,
                        telemetryProperties,
                        telemetryMetrics);
                }
            }
            else
            {
                // Fallback to original logging for non-typed results
                LogLegacyResult(response, requestName, requestId, elapsedMs, startTime);
            }
        }
        else
        {
            // Handle non-Result responses
            _logger.LogInformation("Request {RequestName} with ID {RequestId} completed in {ElapsedMs}ms",
                requestName, requestId, elapsedMs);
            
            telemetryProperties["ResponseType"] = response?.GetType().Name ?? "null";
            telemetryProperties["IsResult"] = "false";
            
            // Application Insights telemetry for non-Result responses
            if (_telemetryClient != null)
            {
                _telemetryClient.TrackEvent($"Request.{requestName}.NonResult", telemetryProperties, telemetryMetrics);
            }
        }

        // Performance warnings using new extensions
        if (elapsedMs > 1000) // More than 1 second
        {
            _logger.LogWarning("Slow operation detected: {RequestName} with ID {RequestId} took {ElapsedMs}ms",
                requestName, requestId, elapsedMs);
                
            // Track slow operations in Application Insights
            if (_telemetryClient != null)
            {
                var slowOpProperties = new Dictionary<string, string>(telemetryProperties)
                {
                    ["PerformanceIssue"] = "SlowOperation",
                    ["ThresholdMs"] = "1000"
                };
                
                _telemetryClient.TrackEvent($"Performance.SlowOperation.{requestName}", 
                    slowOpProperties, telemetryMetrics);
            }
        }
    }

    /// <summary>
    /// Attempts to create a typed Result from a response for use with observability extensions.
    /// </summary>
    private static bool TryCreateTypedResult<T>(T response, out IResult typedResult)
    {
        typedResult = null!;
        
        if (response is IResult result)
        {
            typedResult = result;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Fallback to legacy logging for non-typed results or when observability extensions fail.
    /// </summary>
    private void LogLegacyResult<T>(
        T response,
        string requestName,
        Guid requestId,
        long elapsedMs,
        DateTimeOffset startTime)
    {
        if (response is not IResult result) return;
        
        var logData = new Dictionary<string, object>
        {
            ["RequestName"] = requestName,
            ["RequestId"] = requestId,
            ["ElapsedMs"] = elapsedMs,
            ["StartTime"] = startTime,
            ["CompletedAt"] = DateTimeOffset.UtcNow
        };

        logData["IsSuccess"] = result.IsSuccess;
        logData["IsFailure"] = result.IsFailure;

        if (result.IsSuccess)
        {
            _logger.LogInformation("Request {RequestName} with ID {RequestId} completed successfully in {ElapsedMs}ms",
                requestName, requestId, elapsedMs);

            // Log additional details for generic Result<T>
            if (TryGetResultValue(response, out var value))
            {
                logData["ResultType"] = value?.GetType().Name ?? "null";
                if (ShouldLogResultValue(value))
                {
                    // Mask sensitive data in response value
                    var maskedValue = _sensitiveDataMasker.MaskSensitiveData(value);
                    logData["MaskedResultValue"] = maskedValue;
                }
            }
        }
        else
        {
            var error = GetResultError(result);
            logData["ErrorCode"] = error?.Code;
            logData["ErrorType"] = error?.Type.ToString();
            logData["ErrorMessage"] = error?.Message;

            // Log at different levels based on error type
            var logLevel = GetLogLevelForError(error);
            
            _logger.Log(logLevel, 
                "Request {RequestName} with ID {RequestId} failed in {ElapsedMs}ms - {ErrorCode}: {ErrorMessage}",
                requestName, requestId, elapsedMs, error?.Code, error?.Message);
        }
    }

    /// <summary>
    /// Attempts to extract the value from a Result{T} using reflection.
    /// </summary>
    private static bool TryGetResultValue<T>(T response, out object? value)
    {
        value = null;

        if (response == null)
            return false;

        var responseType = response.GetType();
        
        // Check if it's a generic Result<T>
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueProperty = responseType.GetProperty(nameof(Result<object>.Value));
            var isSuccessProperty = responseType.GetProperty(nameof(Result<object>.IsSuccess));
            
            if (valueProperty != null && isSuccessProperty != null)
            {
                var isSuccess = (bool)isSuccessProperty.GetValue(response)!;
                if (isSuccess)
                {
                    value = valueProperty.GetValue(response);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the error from a failed Result.
    /// </summary>
    private static Error? GetResultError(IResult result)
    {
        if (!result.IsFailure)
            return null;

        try
        {
            return result.Error;
        }
        catch
        {
            return null; // Error property might not be accessible
        }
    }

    /// <summary>
    /// Determines the appropriate log level based on error type.
    /// </summary>
    private static LogLevel GetLogLevelForError(Error? error)
    {
        return error?.Type switch
        {
            ErrorType.Validation => LogLevel.Warning,
            ErrorType.NotFound => LogLevel.Information,
            ErrorType.Unauthorized => LogLevel.Warning,
            ErrorType.Forbidden => LogLevel.Warning,
            ErrorType.BusinessRule => LogLevel.Warning,
            ErrorType.Conflict => LogLevel.Warning,
            ErrorType.Cancelled => LogLevel.Information,
            ErrorType.Internal => LogLevel.Error,
            ErrorType.External => LogLevel.Error,
            ErrorType.Aggregate => LogLevel.Error,
            _ => LogLevel.Error
        };
    }

    /// <summary>
    /// Determines if the result value should be logged based on its type and size.
    /// </summary>
    private static bool ShouldLogResultValue(object? value)
    {
        if (value == null)
            return true;

        var valueType = value.GetType();

        // Log primitive types and strings (if not too long)
        if (valueType.IsPrimitive || valueType == typeof(string))
        {
            if (value is string str && str.Length > 200)
                return false; // Don't log very long strings
            
            return true;
        }

        // Don't log complex objects to avoid verbose logs
        return false;
    }
}