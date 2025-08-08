using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Fallback lightweight logger. It only logs when there is NO current Activity
/// (i.e., ObservabilityPipelineBehavior is not active). Otherwise it's a no-op.
/// This avoids duplicate logs/metrics and keeps tests/dev simple.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private const int SlowMsThreshold = 1000; // warn when over 1s

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        if (elapsedMs > SlowMsThreshold)
            _logger.LogWarning("Slow {RequestType}: {ElapsedMs} ms", reqType, elapsedMs);
        else
            _logger.LogInformation("Handled {RequestType} in {ElapsedMs} ms", reqType, elapsedMs);

        return response;
    }
}