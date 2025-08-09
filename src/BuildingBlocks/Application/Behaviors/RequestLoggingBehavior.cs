using System.Diagnostics;
using BuildingBlocks.Application.Configuration;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Fallback lightweight logger. It only logs when there is NO current Activity
/// (i.e., ObservabilityPipelineBehavior is not active). Otherwise it's a no-op.
/// This avoids duplicate logs/metrics and keeps tests/dev simple.
/// </summary>
public sealed class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private readonly ILogger<RequestLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly LoggingOptions _options;

    public RequestLoggingBehavior(
        ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger,
        IOptions<LoggingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new LoggingOptions();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If an Activity exists (Observability is active), skip to avoid duplicate logging.
        if (Activity.Current is not null)
            return await next();

        var reqType = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestType}", reqType);

        var response = await next();

        sw.Stop();
        var elapsedMs = sw.ElapsedMilliseconds;

        if (elapsedMs > _options.SlowRequestThresholdMs)
            _logger.LogWarning("Slow {RequestType}: {ElapsedMs} ms", reqType, elapsedMs);
        else
            _logger.LogInformation("Handled {RequestType} in {ElapsedMs} ms", reqType, elapsedMs);

        return response;
    }
}