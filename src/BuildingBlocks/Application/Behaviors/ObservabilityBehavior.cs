using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Single source of truth for tracing + metrics + structured logging.
/// CorrelationId == W3C TraceId (Activity.TraceId). No custom correlation provider.
/// Emits low-cardinality metrics and result-aware span tags.
/// </summary>
public sealed class ObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : IResult
{
    private readonly ILogger<ObservabilityBehavior<TRequest, TResponse>> _logger;

    public ObservabilityBehavior(ILogger<ObservabilityBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestCategory = request switch
        {
            ICommand<TResponse> => "Command",
            IQuery<TResponse>   => "Query",
            _                   => "Request"
        };

        var start = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        using var activity = Instrumentation.ActivitySource.StartActivity(
            $"Observability.{requestCategory}.{requestName}",
            ActivityKind.Internal);

        var traceId = (activity ?? Activity.Current)?.TraceId.ToString() ?? "none";
        var spanId  = (activity ?? Activity.Current)?.SpanId.ToString()  ?? "none";

        if (activity is not null)
        {
            activity.SetTag("operation.name", $"{requestCategory}.{requestName}");
            activity.SetTag("axon.request.id", request.RequestId.ToString());
            activity.SetTag("axon.request.type", requestName);
            activity.SetTag("axon.request.category", requestCategory);
            activity.SetTag("axon.request.timestamp", request.RequestedAt.ToString("O"));
            activity.SetTag("axon.correlation.id", traceId); // == trace_id
            activity.SetTag("trace.trace_id", activity.TraceId.ToString());
            activity.SetTag("trace.span_id", activity.SpanId.ToString());

            if (request.Metadata?.Count > 0)
                activity.SetTag("axon.metadata.count", request.Metadata.Count);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"]     = requestName,
            ["RequestCategory"] = requestCategory,
            ["TraceId"]         = traceId,
            ["SpanId"]          = spanId,
            ["RequestId"]       = request.RequestId,
            ["StartTime"]       = start
        });

        _logger.LogDebug("Starting {Category} {Type} (TraceId={TraceId}, RequestId={RequestId})",
            requestCategory, requestName, traceId, request.RequestId);

        try
        {
            var response = await next(); // MediatR delegate has no token parameter
            sw.Stop();

            var elapsedMs = sw.ElapsedMilliseconds;
            var outcome   = response.IsSuccess ? "success" : "failure";

            if (activity is not null)
            {
                activity.SetTag("axon.outcome", outcome);
                activity.SetTag("axon.duration.ms", elapsedMs);
                activity.SetTag(TelemetryTags.Tracing.Otel.StatusCode, response.IsSuccess ? "OK" : "ERROR");
                if (response.IsFailure && response.Error is not null)
                {
                    activity.SetTag("axon.error.type", response.Error.Type.ToString());
                    activity.SetTag("axon.error.code", response.Error.Code);
                    activity.SetTag("error.type", response.Error.Type.ToString());
                    activity.SetTag("error.message", response.Error.Message);
                    activity.SetStatus(ActivityStatusCode.Error, response.Error.Message);
                    activity.SetTag(TelemetryTags.Tracing.Otel.StatusDescription, response.Error.Message);
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }

            var tags = new TagList
            {
                { "axon.request.type", requestName },
                { "axon.request.category", requestCategory },
                { "axon.outcome", outcome },
                { "axon.metadata.has_data", (request.Metadata?.Count > 0) ? "true" : "false" }
            };

            Instrumentation.RequestCounter.Add(1, tags);
            Instrumentation.RequestDuration.Record(elapsedMs, tags);
            if (response.IsFailure) Instrumentation.RequestErrors.Add(1, tags);

            _logger.LogInformation("Request {RequestType} ({RequestCategory}) completed with {Outcome} in {ElapsedMs}ms",
                requestName, requestCategory, outcome, elapsedMs);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            activity?.SetTag("axon.outcome", "cancelled");
            activity?.SetStatus(ActivityStatusCode.Error, "Request was cancelled");

            var tags = new TagList
            {
                { "axon.request.type", requestName },
                { "axon.request.category", requestCategory },
                { "axon.outcome", "cancelled" }
            };

            Instrumentation.RequestCounter.Add(1, tags);
            Instrumentation.RequestDuration.Record(elapsedMs, tags);
            Instrumentation.RequestCancelled.Add(1, tags);

            _logger.LogWarning("{RequestCategory} {RequestType} was cancelled after {ElapsedMs}ms",
                requestCategory, requestName, elapsedMs);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            activity?.SetTag("axon.outcome", "exception");
            activity?.SetTag("axon.exception.type", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent(
                TelemetryTags.Tracing.Exception.EventName,
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    [TelemetryTags.Tracing.Exception.Type] = ex.GetType().FullName,
                    [TelemetryTags.Tracing.Exception.Message] = ex.Message,
                    [TelemetryTags.Tracing.Exception.Stacktrace] = ex.StackTrace ?? string.Empty
                }));

            var tags = new TagList
            {
                { "axon.request.type", requestName },
                { "axon.request.category", requestCategory },
                { "axon.outcome", "exception" }
            };

            Instrumentation.RequestCounter.Add(1, tags);
            Instrumentation.RequestDuration.Record(elapsedMs, tags);
            Instrumentation.RequestErrors.Add(1, tags);

            _logger.LogError(ex, "{RequestCategory} {RequestType} failed after {ElapsedMs}ms",
                requestCategory, requestName, elapsedMs);
            throw;
        }
    }

    private static class Instrumentation
    {
        public static readonly ActivitySource ActivitySource =
            new(TelemetryTags.Tracing.Application.AppService);

        public static readonly Meter Meter =
            new(TelemetryTags.Metrics.Application.AppService);

        public static readonly Counter<long> RequestCounter =
            Meter.CreateCounter<long>("axon.observability.requests.total", description: "Total requests");

        public static readonly Histogram<double> RequestDuration =
            Meter.CreateHistogram<double>("axon.observability.request.duration", unit: "ms", description: "Request duration (ms)");

        public static readonly Counter<long> RequestErrors =
            Meter.CreateCounter<long>("axon.observability.requests.errors.total", description: "Failed requests");

        public static readonly Counter<long> RequestCancelled =
            Meter.CreateCounter<long>("axon.observability.requests.cancelled.total", description: "Cancelled requests");
    }
}
