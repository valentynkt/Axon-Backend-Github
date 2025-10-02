using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Processors;

/// <summary>
/// Pre-processor that logs incoming requests with trace information
/// </summary>
public sealed class LoggingPreProcessor : IGlobalPreProcessor
{
    private readonly ILogger<LoggingPreProcessor> _logger;

    public LoggingPreProcessor(ILogger<LoggingPreProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var endpointName = context.HttpContext.GetEndpoint()?.DisplayName ?? "Unknown";

        _logger.LogInformation(
            "Request received for {EndpointName} (method={Method}, path={Path}, traceId={TraceId})",
            endpointName,
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        return Task.CompletedTask;
    }
}
