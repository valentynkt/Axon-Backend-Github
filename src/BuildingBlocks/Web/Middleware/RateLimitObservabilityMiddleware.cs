using BuildingBlocks.Application.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Middleware;

/// <summary>
/// Middleware to add observability and logging for rate limiting events
/// </summary>
public class RateLimitObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitObservabilityMiddleware> _logger;

    public RateLimitObservabilityMiddleware(RequestDelegate next, ILogger<RateLimitObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Check if the response is a rate limit rejection (429)
        if (context.Response.StatusCode == 429)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var endpoint = context.Request.Path.ToString();
            var correlationId = context.Items["CorrelationId"]?.ToString();

            // Increment rate limit metrics
            Instrumentation.RateLimitHits.Add(1, new KeyValuePair<string, object?>("ip", ipAddress), new KeyValuePair<string, object?>("endpoint", endpoint));

            // Log rate limit violation with structured data
            _logger.LogWarning("Rate limit exceeded for IP {IpAddress} on endpoint {Endpoint} with correlation {CorrelationId}", 
                ipAddress, endpoint, correlationId);
        }
    }
}