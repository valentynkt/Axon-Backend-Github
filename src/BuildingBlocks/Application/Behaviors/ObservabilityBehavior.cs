// /BuildingBlocks/Application/Behaviors/ObservabilityBehavior.cs
#nullable enable
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Single source of truth for tracing + metrics + structured logging over Result&lt;TValue, Error&gt;.
/// Use this instead of a separate logging behavior.
/// - Creates/uses an Activity (W3C TraceContext)
/// - Emits low-cardinality tags and OTel metrics
/// - Logs success/failure with duration; warns on slow requests
/// </summary>
public sealed class ObservabilityBehavior<TRequest, TValue>
    : IPipelineBehavior<TRequest, Result<TValue, Error>>
    where TRequest : IAxonRequest
{

    private readonly ILogger<ObservabilityBehavior<TRequest, TValue>> _logger;
    private readonly ObservabilityOptions _options;
    
    public ObservabilityBehavior(ILogger<ObservabilityBehavior<TRequest, TValue>> logger, IOptions<ObservabilityOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result<TValue, Error>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue, Error>> next,
        CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var category = request switch
        {
            IQuery<TValue>   => "Query",
            ICommand<TValue> => "Command",
            _                => "Request"
        };

        var sw = Stopwatch.StartNew();
        using var activity = Instrumentation.ActivitySource.StartActivity($"Application.{category}.{name}", ActivityKind.Internal);
        activity?.SetTag("axon.request.type", name);
        activity?.SetTag("axon.request.category", category);
        activity?.SetTag("axon.request.id", request.RequestId);
        activity?.SetTag("axon.request.at", request.RequestedAt);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["request.type"]     = name,
            ["request.category"] = category,
            ["trace.id"]         = (activity ?? Activity.Current)?.TraceId.ToString(),
            ["request.id"]       = request.RequestId
        });

        try
        {
            var result = await next();
            sw.Stop();

            var ms = sw.ElapsedMilliseconds;
            var outcome = result.IsSuccess ? "success" : "failure";

            activity?.SetTag("axon.outcome", outcome);
            activity?.SetTag("axon.duration.ms", ms);
            activity?.SetStatus(result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
                result.IsFailure ? result.Error.Message : null);

            var tags = new TagList
            {
                { "axon.request.type", name },
                { "axon.request.category", category },
                { "axon.outcome", outcome }
            };
            Instrumentation.Requests.Add(1, tags);
            Instrumentation.Duration.Record(sw.Elapsed.TotalMilliseconds, tags);
            if (result.IsFailure) Instrumentation.Failures.Add(1, tags);

            if (result.IsSuccess)
            {
                if (ms >= _options.SlowRequestWarningMs)
                    _logger.LogWarning("{Category} {Type} succeeded but slow: {Ms} ms", category, name, ms);
                else
                    _logger.LogInformation("{Category} {Type} succeeded in {Ms} ms", category, name, ms);
            }
            else
            {
                _logger.LogWarning("{Category} {Type} failed ({Code}) in {Ms} ms",
                    category, name, result.Error.Code, ms);
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            var ms = sw.ElapsedMilliseconds;

            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            Instrumentation.Cancelled.Add(1, new TagList { { "axon.request.type", name }, { "axon.request.category", category } });
            _logger.LogWarning("{Category} {Type} cancelled after {Ms} ms", category, name, ms);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var ms = sw.ElapsedMilliseconds;

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = ex.GetType().FullName!,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace ?? string.Empty
            }));
            Instrumentation.Failures.Add(1, new TagList { { "axon.request.type", name }, { "axon.request.category", category } });
            _logger.LogError(ex, "{Category} {Type} threw after {Ms} ms", category, name, ms);
            throw;
        }
    }
}
