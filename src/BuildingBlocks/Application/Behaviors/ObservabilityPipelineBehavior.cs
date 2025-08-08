using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Http;
using IResult = BuildingBlocks.Core.Functional.Results.IResult;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Epic 04 Story 03 - Enhanced ObservabilityPipelineBehavior with W3C TraceContext and Metadata Integration.
/// Provides comprehensive telemetry with Result<T> awareness, metadata enrichment,
/// W3C Trace Context support, cache information tracking, and comprehensive metrics.
/// Integrates with Epic 04 Story 01 metadata support and Story 02 caching.
/// </summary>
public sealed class ObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : IResult
{
    private readonly ILogger<ObservabilityBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    
    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new("Axon.Application");
    private static readonly Meter Meter = new("Axon.Application");
    
    // Metrics instruments - Epic 05 specifications with enhanced metadata support
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "axon.requests.total",
        description: "Total number of requests");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "axon.requests.duration",
        unit: "ms",
        description: "Request duration in milliseconds");
    private static readonly Counter<long> RequestErrors = Meter.CreateCounter<long>(
        "axon.requests.errors",
        description: "Total number of request errors");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "axon.cache.hits",
        description: "Total number of cache hits");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "axon.cache.misses",
        description: "Total number of cache misses");

    public ObservabilityBehavior(
        ILogger<ObservabilityBehavior<TRequest, TResponse>> logger,
        ICorrelationIdProvider correlationIdProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).Name;
        var correlationId = _correlationIdProvider.GetOrGenerate();
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        // Start OpenTelemetry Activity with enhanced naming
        using var activity = ActivitySource.StartActivity($"CQRS.{GetRequestCategory(request)}.{requestName}", ActivityKind.Internal);
        
        if (activity != null)
        {
            // Epic 04 Story 03: Enhanced activity enrichment with metadata and W3C TraceContext
            EnrichActivityWithRequest(activity, request, correlationId, startTime);
            EnrichActivityWithMetadata(activity, request.Metadata);
            
            // Epic 04 Story 02: Add cache information for queries
            if (request is IQuery<object> query)
            {
                EnrichActivityWithCacheInfo(activity, query);
            }
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["CorrelationId"] = correlationId,
            ["StartTime"] = startTime,
            ["RequestId"] = request.RequestId,
            ["TraceId"] = request.TraceId ?? "none",
            ["SpanId"] = request.SpanId ?? "none"
        });

        _logger.LogDebug("Starting {RequestType} execution with correlation ID {CorrelationId} and request ID {RequestId}",
            requestName, correlationId, request.RequestId);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            // Record enhanced telemetry with metadata context
            RecordTelemetry(response, request, requestName, stopwatch, activity, startTime);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            RecordCancelledRequest(requestName, stopwatch, activity);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordExceptionRequest(requestName, ex, stopwatch, activity, request.Metadata);
            throw;
        }
    }

    private static string GetRequestCategory(TRequest request)
    {
        return request switch
        {
            ICommand<TResponse> => "Command",
            IQuery<TResponse> => "Query", 
            _ => "Request"
        };
    }

    private void EnrichActivityWithRequest(Activity activity, TRequest request, string correlationId, DateTimeOffset startTime)
    {
        // Basic request information
        activity.SetTag("axon.request.id", request.RequestId.ToString());
        activity.SetTag("axon.request.type", typeof(TRequest).Name);
        activity.SetTag("axon.request.namespace", typeof(TRequest).Namespace);
        activity.SetTag("axon.request.category", GetRequestCategory(request));
        activity.SetTag("axon.request.timestamp", request.RequestedAt.ToString("O"));
        activity.SetTag("axon.correlation.id", correlationId);
        activity.SetTag("axon.start.time", startTime.ToString("O"));

        // Epic 04 Story 01: W3C TraceContext integration
        if (!string.IsNullOrEmpty(request.TraceId))
        {
            activity.SetTag("axon.trace.id", request.TraceId);
            activity.SetTag("axon.span.id", request.SpanId);
            activity.SetTag("axon.parent_span.id", request.ParentSpanId);
        }
    }

    private void EnrichActivityWithMetadata(Activity activity, IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata == null || !metadata.Any()) return;

        // Add count of metadata items
        activity.SetTag("axon.metadata.count", metadata.Count);

        // Add sanitized metadata as activity tags
        foreach (var (key, value) in metadata)
        {
            if (IsSafeForTelemetry(key, value))
            {
                activity.SetTag($"axon.metadata.{SanitizeTagName(key)}", SanitizeTagValue(value));
            }
        }

        // Special handling for common metadata keys
        if (metadata.TryGetValue("TenantId", out var tenantId))
        {
            activity.SetTag("axon.tenant.id", tenantId?.ToString());
        }

        if (metadata.TryGetValue("UserId", out var userId))
        {
            activity.SetTag("axon.user.id", userId?.ToString());
        }

        if (metadata.TryGetValue("ClientId", out var clientId))
        {
            activity.SetTag("axon.client.id", clientId?.ToString());
        }

        if (metadata.TryGetValue("Version", out var version))
        {
            activity.SetTag("axon.version", version?.ToString());
        }
    }

    private void EnrichActivityWithCacheInfo(Activity activity, IQuery<object> query)
    {
        activity.SetTag("axon.query.cache.enabled", query.UseCache);
        
        if (query.UseCache)
        {
            activity.SetTag("axon.query.cache.duration", query.CacheDuration?.TotalMinutes);
            activity.SetTag("axon.query.cache.key_prefix", query.CacheKeyPrefix);
        }
    }

    private void RecordTelemetry(TResponse response, TRequest request, string requestName, Stopwatch stopwatch, Activity? activity, DateTimeOffset startTime)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var outcome = response.IsSuccess ? "success" : "failure";
        var errorType = response.IsFailure ? response.Error?.Type.ToString() : null;
        var errorCode = response.IsFailure ? response.Error?.Code : null;
        var requestCategory = GetRequestCategory(request);

        // Enhanced OpenTelemetry Activity tags
        if (activity != null)
        {
            activity.SetTag("axon.outcome", outcome);
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag("axon.completed.at", DateTimeOffset.UtcNow.ToString("O"));
            
            if (response.IsFailure)
            {
                activity.SetTag("axon.error.type", errorType);
                activity.SetTag("axon.error.code", errorCode);
                activity.SetStatus(ActivityStatusCode.Error, response.Error?.Message);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            // Add metadata context for error correlation
            if (response.IsFailure && request.Metadata.Any())
            {
                activity.SetTag("axon.error.has_metadata", true);
                activity.SetTag("axon.error.metadata_count", request.Metadata.Count);
            }
        }

        // Enhanced metrics with metadata context
        var tags = CreateMetricTags(requestName, requestCategory, outcome, errorType, request.Metadata);

        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsedMs, tags);

        if (response.IsFailure)
        {
            RequestErrors.Add(1, tags);
        }

        // Enhanced structured logging with metadata context
        var logData = new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["RequestCategory"] = requestCategory,
            ["Outcome"] = outcome,
            ["ElapsedMs"] = elapsedMs,
            ["ErrorCode"] = errorCode ?? "none",
            ["RequestId"] = request.RequestId,
            ["MetadataCount"] = request.Metadata.Count
        };

        // Add safe metadata to log context
        foreach (var (key, value) in request.Metadata.Take(5)) // Limit to avoid log pollution
        {
            if (IsSafeForTelemetry(key, value))
            {
                logData[$"Metadata_{SanitizeTagName(key)}"] = SanitizeTagValue(value);
            }
        }

        using var logScope = _logger.BeginScope(logData);
        _logger.LogInformation(
            "Request {RequestType} ({RequestCategory}) completed with outcome {Outcome} in {ElapsedMs}ms - Error: {ErrorCode}",
            requestName, requestCategory, outcome, elapsedMs, errorCode);
    }

    private void RecordCancelledRequest(string requestName, Stopwatch stopwatch, Activity? activity)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        if (activity != null)
        {
            activity.SetTag("axon.outcome", "cancelled");
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetStatus(ActivityStatusCode.Error, "Request was cancelled");
        }

        var tags = new TagList
        {
            { "axon.request.type", requestName },
            { "axon.outcome", "cancelled" },
            { "axon.error.type", "cancellation" }
        };

        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsedMs, tags);
        RequestErrors.Add(1, tags);

        _logger.LogWarning("Request {RequestType} was cancelled after {ElapsedMs}ms",
            requestName, elapsedMs);
    }

    private void RecordExceptionRequest(string requestName, Exception exception, Stopwatch stopwatch, Activity? activity, IReadOnlyDictionary<string, object> metadata)
    {
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var exceptionType = exception.GetType().Name;

        if (activity != null)
        {
            activity.SetTag("axon.outcome", "exception");
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag("axon.exception.type", exceptionType);
            activity.SetTag("axon.exception.message", exception.Message);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            
            // Add metadata context for exception correlation
            if (metadata.Any())
            {
                activity.SetTag("axon.exception.has_metadata", true);
                activity.SetTag("axon.exception.metadata_count", metadata.Count);
            }
        }

        var tags = new TagList
        {
            { "axon.request.type", requestName },
            { "axon.outcome", "exception" },
            { "axon.error.type", exceptionType }
        };

        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsedMs, tags);
        RequestErrors.Add(1, tags);

        _logger.LogError(exception, "Request {RequestType} failed with exception after {ElapsedMs}ms",
            requestName, elapsedMs);
    }

    private TagList CreateMetricTags(string requestName, string requestCategory, string outcome, string? errorType, IReadOnlyDictionary<string, object> metadata)
    {
        var tags = new TagList
        {
            { "axon.request.type", requestName },
            { "axon.request.category", requestCategory },
            { "axon.outcome", outcome },
            { "axon.error.type", errorType ?? "none" },
            { "axon.metadata.has_data", metadata.Any() ? "true" : "false" }
        };

        // Add tenant context if available
        if (metadata.TryGetValue("TenantId", out var tenantId))
        {
            tags.Add("axon.tenant.id", tenantId?.ToString() ?? "unknown");
        }

        return tags;
    }

    private static bool IsSafeForTelemetry(string key, object? value)
    {
        // Exclude sensitive keys
        var sensitiveKeys = new[] { "password", "secret", "token", "key", "auth", "credential" };
        if (sensitiveKeys.Any(sensitive => key.Contains(sensitive, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Exclude null or very large values
        if (value == null) return false;
        
        var valueString = value.ToString();
        if (string.IsNullOrEmpty(valueString) || valueString.Length > 100)
            return false;

        return true;
    }

    private static string SanitizeTagName(string key)
    {
        // Ensure tag names are valid for OpenTelemetry
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
}

/// <summary>
/// Correlation ID provider interface for Epic 05 ObservabilityBehavior.
/// </summary>
public interface ICorrelationIdProvider
{
    string GetOrGenerate();
    void Set(string correlationId);
}

/// <summary>
/// Implementation of correlation ID provider with HTTP context integration.
/// </summary>
public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly AsyncLocal<string?> _correlationId = new();

    public CorrelationIdProvider(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetOrGenerate()
    {
        // Check HTTP header first, then AsyncLocal, then generate
        var httpCorrelationId = _httpContextAccessor?.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrEmpty(httpCorrelationId))
        {
            _correlationId.Value = httpCorrelationId;
            return httpCorrelationId;
        }

        if (!string.IsNullOrEmpty(_correlationId.Value))
        {
            return _correlationId.Value;
        }

        var newCorrelationId = GenerateCorrelationId();
        _correlationId.Value = newCorrelationId;
        return newCorrelationId;
    }

    public void Set(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    private static string GenerateCorrelationId() =>
        $"axon-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
}