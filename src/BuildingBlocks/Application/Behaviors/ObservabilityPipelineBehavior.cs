using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Observability pipeline with pure W3C TraceContext:
/// - CorrelationId == Activity.TraceId (no custom provider)
/// - OpenTelemetry spans + low-cardinality metrics
/// - Result-aware tagging (IResult)
/// </summary>
public sealed class ObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : IResult
{
    private readonly ILogger<ObservabilityBehavior<TRequest, TResponse>> _logger;

    public ObservabilityBehavior(
        ILogger<ObservabilityBehavior<TRequest, TResponse>> logger)
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
        var requestCategory = request switch
        {
            ICommand<TResponse> => "Command",
            IQuery<TResponse> => "Query",
            _ => "Request"
        };

        var startTime = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        // Create an INTERNAL child span if a parent exists; otherwise root span.
        using var activity = ObservabilityInstrumentation.ActivitySource.StartActivity(
            $"Observability.{requestCategory}.{requestName}",
            ActivityKind.Internal);

        // CorrelationId is ALWAYS the W3C TraceId
        var correlationId = (activity ?? Activity.Current)?.TraceId.ToString() ?? "none";
        var spanId = (activity ?? Activity.Current)?.SpanId.ToString() ?? "none";

        if (activity is not null)
        {
            EnrichActivityWithRequest(activity, request, correlationId, startTime, requestCategory);
            EnrichActivityWithMetadata(activity, request.Metadata);
            TryEnrichActivityWithCacheInfo(activity, request);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = requestName,
            ["RequestCategory"] = requestCategory,
            ["CorrelationId"] = correlationId,                // == TraceId
            ["TraceId"] = correlationId,
            ["SpanId"] = spanId,
            ["StartTime"] = startTime,
            ["RequestId"] = request.RequestId
        });

        _logger.LogDebug("Starting {Category} {Type} (TraceId={TraceId}, SpanId={SpanId}, RequestId={RequestId})",
            requestCategory, requestName, correlationId, spanId, request.RequestId);

        try
        {
            var response = await next(); // MediatR delegate has no token arg (CA2016 suppressed globally)
            sw.Stop();

            RecordTelemetry(response, request, requestName, requestCategory, sw.ElapsedMilliseconds, activity);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            RecordCancelled(requestName, requestCategory, sw.ElapsedMilliseconds, activity);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordException(requestName, requestCategory, ex, sw.ElapsedMilliseconds, activity, request.Metadata);
            throw;
        }
    }

    private static void EnrichActivityWithRequest(Activity activity, TRequest request, string correlationId, DateTimeOffset startTime, string category)
    {
        activity.SetTag("operation.name", $"{category}.{typeof(TRequest).Name}");
        activity.SetTag("axon.request.id", request.RequestId.ToString());
        activity.SetTag("axon.request.type", typeof(TRequest).Name);
        activity.SetTag("axon.request.namespace", typeof(TRequest).Namespace ?? "unknown");
        activity.SetTag("axon.request.category", category);
        activity.SetTag("axon.request.timestamp", request.RequestedAt.ToString("O"));

        // W3C correlation identifiers
        activity.SetTag("axon.correlation.id", correlationId); // == trace_id
        activity.SetTag("trace.trace_id", activity.TraceId.ToString());
        activity.SetTag("trace.span_id", activity.SpanId.ToString());

        // If request carried prior IDs (e.g., from external systems), keep as tags
        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            activity.SetTag("axon.upstream.trace_id", request.TraceId);
            activity.SetTag("axon.upstream.span_id", request.SpanId);
            activity.SetTag("axon.upstream.parent_span_id", request.ParentSpanId);
        }

        activity.SetTag("axon.start.time", startTime.ToString("O"));
    }

    private static void EnrichActivityWithMetadata(Activity activity, IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return;

        activity.SetTag("axon.metadata.count", metadata.Count);

        foreach (var kv in metadata)
        {
            if (IsSafeForTelemetry(kv.Key, kv.Value))
            {
                activity.SetTag($"axon.metadata.{SanitizeTagName(kv.Key)}", SanitizeTagValue(kv.Value));
            }
        }

        if (metadata.TryGetValue("TenantId", out var tenantId))
            activity.SetTag("axon.tenant.id", tenantId?.ToString());

        if (metadata.TryGetValue("Version", out var version))
            activity.SetTag("axon.version", version?.ToString());
    }

    private static void TryEnrichActivityWithCacheInfo(Activity activity, TRequest request)
    {
        // Reflection-based "duck typing" to avoid taking a dependency on a marker interface
        var t = request.GetType();
        var useCacheProp = t.GetProperty("UseCache");
        if (useCacheProp is null) return;

        var enabled = useCacheProp.GetValue(request) as bool? ?? false;
        activity.SetTag("axon.query.cache.enabled", enabled);

        if (!enabled) return;

        if (t.GetProperty("CacheDuration")?.GetValue(request) is TimeSpan ttl)
            activity.SetTag("axon.query.cache.duration", ttl.TotalMinutes);

        if (t.GetProperty("CacheKeyPrefix")?.GetValue(request) is string prefix)
            activity.SetTag("axon.query.cache.key_prefix", prefix);
    }

    private void RecordTelemetry(TResponse response, TRequest request, string name, string category, long elapsedMs, Activity? activity)
    {
        var outcome = response.IsSuccess ? "success" : "failure";
        var errorType = response.IsFailure ? response.Error?.Type.ToString() : null;
        var errorCode = response.IsFailure ? response.Error?.Code : null;

        if (activity is not null)
        {
            activity.SetTag("axon.outcome", outcome);
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag("axon.completed.at", DateTimeOffset.UtcNow.ToString("O"));
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, response.IsSuccess ? "OK" : "ERROR");

            if (response.IsFailure)
            {
                activity.SetTag("axon.error.type", errorType);
                activity.SetTag("axon.error.code", errorCode);
                activity.SetTag("error.type", errorType);
                activity.SetTag("error.message", response.Error?.Message);
                activity.SetStatus(ActivityStatusCode.Error, response.Error?.Message);
                activity.SetTag(TelemetryTags.Tracing.Otel.StatusDescription, response.Error?.Message);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            // Generic IResult enrichment
            activity.SetTag("axon.result.is_success", response.IsSuccess);
            if (response.IsFailure && response.Error is not null)
            {
                activity.SetTag("axon.result.error.code", response.Error.Code);
                activity.SetTag("axon.result.error.type", response.Error.Type.ToString());
            }
        }

        // Low-cardinality metric tags (NO tenant/user ids)
        var tags = new TagList
        {
            { "axon.request.type", name },
            { "axon.request.category", category },
            { "axon.outcome", outcome },
            { "axon.error.type", errorType ?? "none" },
            { "axon.metadata.has_data", (request.Metadata?.Count > 0) ? "true" : "false" }
        };

        ObservabilityInstrumentation.RequestCounter.Add(1, tags);
        ObservabilityInstrumentation.RequestDuration.Record(elapsedMs, tags);

        if (response.IsFailure)
            ObservabilityInstrumentation.RequestErrors.Add(1, tags);

        _logger.LogInformation("Request {RequestType} ({RequestCategory}) completed with {Outcome} in {ElapsedMs}ms - Error: {ErrorCode}",
            name, category, outcome, elapsedMs, errorCode ?? "none");
    }

    private void RecordCancelled(string name, string category, long elapsedMs, Activity? activity)
    {
        if (activity is not null)
        {
            activity.SetTag("axon.outcome", "cancelled");
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, "ERROR");
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusDescription, "Request was cancelled");
            activity.SetStatus(ActivityStatusCode.Error, "Request was cancelled");
        }

        var tags = new TagList
        {
            { "axon.request.type", name },
            { "axon.request.category", category },
            { "axon.outcome", "cancelled" },
            { "axon.error.type", "cancellation" }
        };

        ObservabilityInstrumentation.RequestCounter.Add(1, tags);
        ObservabilityInstrumentation.RequestDuration.Record(elapsedMs, tags);
        ObservabilityInstrumentation.RequestCancelled.Add(1, tags);

        _logger.LogWarning("{RequestCategory} {RequestType} was cancelled after {ElapsedMs}ms",
            category, name, elapsedMs);
    }

    private void RecordException(string name, string category, Exception ex, long elapsedMs, Activity? activity, IReadOnlyDictionary<string, object>? metadata)
    {
        var exceptionType = ex.GetType().Name;

        if (activity is not null)
        {
            activity.SetTag("axon.outcome", "exception");
            activity.SetTag("axon.duration.ms", elapsedMs);
            activity.SetTag("axon.exception.type", exceptionType);
            activity.SetTag("axon.exception.message", ex.Message);
            activity.SetTag("error.type", exceptionType);
            activity.SetTag("error.message", ex.Message);
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, "ERROR");
            activity.SetTag(TelemetryTags.Tracing.Otel.StatusDescription, ex.Message);
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);

            activity.AddEvent(new ActivityEvent(
                TelemetryTags.Tracing.Exception.EventName,
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    [TelemetryTags.Tracing.Exception.Type] = ex.GetType().FullName,
                    [TelemetryTags.Tracing.Exception.Message] = ex.Message,
                    [TelemetryTags.Tracing.Exception.Stacktrace] = ex.StackTrace ?? string.Empty
                }));

            if (metadata is not null && metadata.Count > 0)
            {
                activity.SetTag("axon.exception.has_metadata", true);
                activity.SetTag("axon.exception.metadata_count", metadata.Count);
            }
        }

        var tags = new TagList
        {
            { "axon.request.type", name },
            { "axon.request.category", category },
            { "axon.outcome", "exception" },
            { "axon.error.type", exceptionType }
        };

        ObservabilityInstrumentation.RequestCounter.Add(1, tags);
        ObservabilityInstrumentation.RequestDuration.Record(elapsedMs, tags);
        ObservabilityInstrumentation.RequestErrors.Add(1, tags);

        _logger.LogError(ex, "{RequestCategory} {RequestType} failed after {ElapsedMs}ms", category, name, elapsedMs);
    }

    private static bool IsSafeForTelemetry(string key, object? value)
    {
        if (value is null) return false;

        var sensitive = new[] { "password", "secret", "token", "key", "auth", "credential" };
        if (sensitive.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase))) return false;

        var s = value.ToString();
        return !string.IsNullOrEmpty(s) && s.Length <= 100;
    }

    private static string SanitizeTagName(string key) =>
        (key ?? string.Empty).ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_");

    private static string SanitizeTagValue(object value)
    {
        var s = value?.ToString() ?? string.Empty;
        return s.Length > 100 ? s[..100] + "..." : s;
    }

    /// <summary> Single, shared instrumentation (no per-generic duplication). </summary>
    private static class ObservabilityInstrumentation
    {
        public static readonly ActivitySource ActivitySource =
            new(TelemetryTags.Tracing.Application.AppService);

        public static readonly Meter Meter =
            new(TelemetryTags.Metrics.Application.AppService);

        public static readonly Counter<long> RequestCounter =
            Meter.CreateCounter<long>("axon.observability.requests.total", description: "Total number of requests processed");

        public static readonly Histogram<double> RequestDuration =
            Meter.CreateHistogram<double>("axon.observability.request.duration", unit: "ms", description: "Request duration in ms");

        public static readonly Counter<long> RequestErrors =
            Meter.CreateCounter<long>("axon.observability.requests.errors.total", description: "Total number of failed requests");

        public static readonly Counter<long> RequestCancelled =
            Meter.CreateCounter<long>("axon.observability.requests.cancelled.total", description: "Total number of cancelled requests");
    }
}
