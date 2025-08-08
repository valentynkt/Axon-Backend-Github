using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Extensions;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Modern Result-aware logging behavior for MediatR pipeline using OpenTelemetry.
/// Provides structured logging with W3C TraceContext support, performance metrics,
/// and comprehensive telemetry without Application Insights dependencies.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type - optimized for Result types</typeparam>
public sealed class ResultLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
{
    private readonly ILogger<ResultLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ISensitiveDataMasker _sensitiveDataMasker;
    
    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(TelemetryTags.Tracing.Application.AppService);
    private static readonly Meter Meter = new(TelemetryTags.Metrics.Application.AppService);
    
    // Metrics instruments with W3C semantic conventions
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "axon.pipeline.requests.total",
        description: "Total number of pipeline requests processed");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "axon.pipeline.request.duration",
        unit: "ms",
        description: "Duration of pipeline request processing");
    private static readonly Counter<long> RequestErrors = Meter.CreateCounter<long>(
        "axon.pipeline.requests.errors.total",
        description: "Total number of failed pipeline requests");

    public ResultLoggingBehavior(
        ILogger<ResultLoggingBehavior<TRequest, TResponse>> logger,
        ISensitiveDataMasker sensitiveDataMasker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sensitiveDataMasker = sensitiveDataMasker ?? throw new ArgumentNullException(nameof(sensitiveDataMasker));
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
        var requestCategory = GetRequestCategory(request);
        var maskedRequest = _sensitiveDataMasker.MaskSensitiveData(request);
        
        // Start OpenTelemetry Activity with W3C TraceContext
        using var activity = ActivitySource.StartActivity($"Pipeline.{requestCategory}.{requestName}");
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        // Enrich Activity with request context
        EnrichActivityWithRequest(activity, request, requestName, requestCategory, startTime);
        
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["RequestCategory"] = requestCategory,
            ["RequestId"] = requestId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
            ["SpanId"] = Activity.Current?.SpanId.ToString(),
            ["StartTime"] = startTime
        });

        _logger.LogInformation("Starting {RequestCategory} {RequestName} with ID {RequestId}", 
            requestCategory, requestName, requestId);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            // Record telemetry and log results
            RecordSuccessfulRequest(response, request, requestName, requestCategory, stopwatch, activity);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            RecordCancelledRequest(requestName, requestCategory, requestId, stopwatch, activity);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordFailedRequest(requestName, requestCategory, requestId, ex, stopwatch, activity);
            throw;
        }
    }

    #region Request Category and Activity Enrichment
    
    private static string GetRequestCategory<T>(T request) where T : IAxonRequest<TResponse>
    {
        return request switch
        {
            ICommand<TResponse> => "Command",
            IQuery<TResponse> => "Query",
            _ => "Request"
        };
    }
    
    private static void EnrichActivityWithRequest<T>(
        Activity? activity,
        T request,
        string requestName,
        string requestCategory,
        DateTimeOffset startTime) where T : IAxonRequest<TResponse>
    {
        if (activity == null) return;
        
        // Standard W3C semantic conventions
        activity.SetTag("operation.name", $"{requestCategory}.{requestName}");
        activity.SetTag("axon.request.type", requestName);
        activity.SetTag("axon.request.category", requestCategory);
        activity.SetTag("axon.request.id", request.RequestId.ToString());
        activity.SetTag("axon.request.timestamp", request.RequestedAt.ToString("O"));
        activity.SetTag("axon.start.time", startTime.ToString("O"));
        
        // W3C TraceContext integration
        if (!string.IsNullOrEmpty(request.TraceId))
        {
            activity.SetTag("axon.trace.id", request.TraceId);
            activity.SetTag("axon.span.id", request.SpanId);
            activity.SetTag("axon.parent_span.id", request.ParentSpanId);
        }
        
        // Add metadata context
        if (request.Metadata.Any())
        {
            activity.SetTag("axon.metadata.count", request.Metadata.Count);
            
            // Add safe metadata as tags (limited for performance)
            foreach (var (key, value) in request.Metadata.Take(5))
            {
                if (IsSafeForTelemetry(key, value))
                {
                    activity.SetTag($"axon.metadata.{SanitizeTagName(key)}", SanitizeTagValue(value));
                }
            }
        }
    }
    
    #endregion

    #region Telemetry Recording Methods
    
    private void RecordSuccessfulRequest<T>(
        T response,
        TRequest request,
        string requestName,
        string requestCategory,
        Stopwatch stopwatch,
        Activity? activity)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var outcome = "success";
        
        if (response is IResult result)
        {
            outcome = result.IsSuccess ? "success" : "failure";
            
            // Use Result telemetry extensions for OpenTelemetry integration
            if (result is Result<object> typedResult)
            {
                typedResult
                    .EnrichActivity(requestName)
                    .LogResult(_logger,
                        $"{requestCategory} {requestName} completed successfully",
                        $"{requestCategory} {requestName} failed");
            }
            
            LogResultOutcome(result, requestName, requestCategory, request.RequestId, elapsedMs, outcome);
        }
        else
        {
            _logger.LogInformation("{RequestCategory} {RequestName} completed successfully in {ElapsedMs}ms",
                requestCategory, requestName, elapsedMs);
        }

        // Record OpenTelemetry metrics
        ResultLoggingBehavior<TRequest, TResponse>.RecordMetrics(requestName, requestCategory, outcome, elapsedMs, null, request.Metadata);
        
        // Complete Activity with success status
        if (activity != null)
        {
            activity.SetTag("axon.outcome", outcome);
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetStatus(outcome == "success" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
        
        // Performance warning
        if (elapsedMs > 1000)
        {
            _logger.LogWarning("Slow operation detected: {RequestCategory} {RequestName} took {ElapsedMs}ms",
                requestCategory, requestName, elapsedMs);
        }
    }
    
    private void RecordCancelledRequest(
        string requestName,
        string requestCategory,
        Guid requestId,
        Stopwatch stopwatch,
        Activity? activity)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var outcome = "cancelled";
        
        if (activity != null)
        {
            activity.SetTag("axon.outcome", outcome);
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetStatus(ActivityStatusCode.Error, "Request was cancelled");
        }

        ResultLoggingBehavior<TRequest, TResponse>.RecordMetrics(requestName, requestCategory, outcome, elapsedMs, "cancellation", new Dictionary<string, object>());
        
        _logger.LogWarning("{RequestCategory} {RequestName} with ID {RequestId} was cancelled after {ElapsedMs}ms",
            requestCategory, requestName, requestId, elapsedMs);
    }
    
    private void RecordFailedRequest(
        string requestName,
        string requestCategory,
        Guid requestId,
        Exception exception,
        Stopwatch stopwatch,
        Activity? activity)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var outcome = "exception";
        var exceptionType = exception.GetType().Name;
        
        if (activity != null)
        {
            activity.SetTag("axon.outcome", outcome);
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag("axon.exception.type", exceptionType);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            
            // Add exception event following W3C conventions
            activity.AddEvent(new ActivityEvent(TelemetryTags.Tracing.Exception.EventName, DateTimeOffset.UtcNow, new ActivityTagsCollection
            {
                [TelemetryTags.Tracing.Exception.Type] = exception.GetType().FullName,
                [TelemetryTags.Tracing.Exception.Message] = exception.Message,
                [TelemetryTags.Tracing.Exception.Stacktrace] = exception.StackTrace
            }));
        }

        ResultLoggingBehavior<TRequest, TResponse>.RecordMetrics(requestName, requestCategory, outcome, elapsedMs, exceptionType, new Dictionary<string, object>());
        
        _logger.LogError(exception, 
            "{RequestCategory} {RequestName} with ID {RequestId} failed with {ExceptionType} after {ElapsedMs}ms",
            requestCategory, requestName, requestId, exceptionType, elapsedMs);
    }

    private void LogResultOutcome(
        IResult result,
        string requestName,
        string requestCategory,
        Guid requestId,
        long elapsedMs,
        string outcome)
    {
        if (result.IsSuccess)
        {
            _logger.LogInformation("{RequestCategory} {RequestName} with ID {RequestId} completed with outcome {Outcome} in {ElapsedMs}ms",
                requestCategory, requestName, requestId, outcome, elapsedMs);
        }
        else
        {
            var error = result.Error;
            var logLevel = GetLogLevelForError(error);
            
            _logger.Log(logLevel,
                "{RequestCategory} {RequestName} with ID {RequestId} completed with outcome {Outcome} in {ElapsedMs}ms - {ErrorCode}: {ErrorMessage}",
                requestCategory, requestName, requestId, outcome, elapsedMs, error.Code, error.Message);
        }
    }
    
    private static void RecordMetrics(
        string requestName,
        string requestCategory,
        string outcome,
        long elapsedMs,
        string? errorType,
        IReadOnlyDictionary<string, object> metadata)
    {
        var tags = new TagList
        {
            { "axon.request.type", requestName },
            { "axon.request.category", requestCategory },
            { "axon.outcome", outcome },
            { "axon.error.type", errorType ?? "none" },
            { "axon.metadata.has_data", metadata.Any() ? "true" : "false" }
        };

        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsedMs, tags);

        if (outcome != "success")
        {
            RequestErrors.Add(1, tags);
        }
    }
    
    private static LogLevel GetLogLevelForError(Error error)
    {
        return error.Type switch
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
    
    #endregion
    
    #region Helper Methods for Safety and Sanitization
    
    private static bool IsSafeForTelemetry(string key, object? value)
    {
        var sensitiveKeys = new[] { "password", "secret", "token", "key", "auth", "credential" };
        if (sensitiveKeys.Any(sensitive => key.Contains(sensitive, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (value == null) return false;
        
        var valueString = value.ToString();
        return !string.IsNullOrEmpty(valueString) && valueString.Length <= 100;
    }
    
    private static string SanitizeTagName(string key)
    {
        return key.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_");
    }
    
    private static string SanitizeTagValue(object value)
    {
        var stringValue = value?.ToString() ?? "";
        return stringValue.Length > 100 ? stringValue[..100] + "..." : stringValue;
    }
    
    #endregion
}