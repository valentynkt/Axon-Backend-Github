using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Processors;

/// <summary>
/// Post-processor that logs successful request completion with trace information
/// </summary>
public sealed class LoggingPostProcessor : IGlobalPostProcessor
{
    private readonly ILogger<LoggingPostProcessor> _logger;

    public LoggingPostProcessor(ILogger<LoggingPostProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var endpoint = httpContext.GetEndpoint();
        var endpointName = endpoint?.DisplayName ?? "Unknown";
        var statusCode = httpContext.Response.StatusCode;

        if (statusCode >= 200 && statusCode < 300)
        {
            _logger.LogInformation(
                "Request completed successfully for {EndpointName} (status={StatusCode}, traceId={TraceId})",
                endpointName,
                statusCode,
                httpContext.TraceIdentifier);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "Request completed with error for {EndpointName} (status={StatusCode}, traceId={TraceId})",
                endpointName,
                statusCode,
                httpContext.TraceIdentifier);
        }

        return Task.CompletedTask;
    }
}
