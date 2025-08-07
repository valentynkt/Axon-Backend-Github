using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;

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

    public ResultLoggingBehavior(ILogger<ResultLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["RequestId"] = requestId,
            ["RequestedAt"] = request.RequestedAt
        });

        _logger.LogInformation("Starting request {RequestName} with ID {RequestId}", requestName, requestId);

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            // Log based on Result pattern
            LogResult(response, requestName, requestId, stopwatch.ElapsedMilliseconds, startTime);

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
    /// Logs the result of the request execution with appropriate log level and details.
    /// </summary>
    private void LogResult<T>(
        T response, 
        string requestName, 
        Guid requestId, 
        long elapsedMs,
        DateTimeOffset startTime)
    {
        var logData = new Dictionary<string, object>
        {
            ["RequestName"] = requestName,
            ["RequestId"] = requestId,
            ["ElapsedMs"] = elapsedMs,
            ["StartTime"] = startTime,
            ["CompletedAt"] = DateTimeOffset.UtcNow
        };

        // Handle Result<T> response types
        if (response is IResult result)
        {
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
                        logData["ResultValue"] = value;
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

                // Log additional error details at debug level
                if (_logger.IsEnabled(LogLevel.Debug) && error?.Details != null)
                {
                    _logger.LogDebug("Error details for {RequestName} with ID {RequestId}: {ErrorDetails}",
                        requestName, requestId, error.Details);
                }
            }
        }
        else
        {
            // Handle non-Result responses
            _logger.LogInformation("Request {RequestName} with ID {RequestId} completed in {ElapsedMs}ms",
                requestName, requestId, elapsedMs);
            
            logData["ResponseType"] = response?.GetType().Name ?? "null";
        }

        // Log performance warning for slow operations
        if (elapsedMs > 1000) // More than 1 second
        {
            _logger.LogWarning("Slow operation detected: {RequestName} with ID {RequestId} took {ElapsedMs}ms",
                requestName, requestId, elapsedMs);
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
            ErrorType.Authorization => LogLevel.Warning,
            ErrorType.BusinessRule => LogLevel.Warning,
            ErrorType.Conflict => LogLevel.Warning,
            ErrorType.Cancellation => LogLevel.Information,
            ErrorType.System => LogLevel.Error,
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