using BuildingBlocks.Application.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BuildingBlocks.Web.Middleware;

/// <summary>
/// Middleware to add observability and logging for rate limiting events
/// </summary>
public class RateLimitObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitObservabilityMiddleware> _logger;
    private readonly ConcurrentDictionary<string, int> _requestCounts = new();
    private readonly ConcurrentDictionary<string, DateTime> _resetTimes = new();

    public RateLimitObservabilityMiddleware(RequestDelegate next, ILogger<RateLimitObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // For rate-limited endpoints, set up headers before processing the request
        var isRateLimitedEndpoint = context.Request.Path.StartsWithSegments("/api/v1/auth/exchange", StringComparison.Ordinal);

        if (isRateLimitedEndpoint)
        {
            // Get IP address, checking X-Forwarded-For header first
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            var partitionKey = $"rate_limited_{ipAddress}";
            var currentTime = DateTime.UtcNow;

            // Initialize or reset counter if needed
            if (!_resetTimes.TryGetValue(partitionKey, out var resetTime) || currentTime > resetTime)
            {
                _requestCounts[partitionKey] = 0;
                _resetTimes[partitionKey] = currentTime.AddMinutes(1);
            }

            // Add response starting callback to add headers at the right time
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
                {
                    // Get current count before incrementing for header calculation
                    var currentCount = _requestCounts.GetValueOrDefault(partitionKey, 0);

                    // Increment request count
                    var newCount = _requestCounts.AddOrUpdate(partitionKey, 1, (key, value) => value + 1);

                    // Calculate remaining based on new count
                    var remaining = Math.Max(0, 10 - newCount);

                    context.Response.Headers["X-RateLimit-Limit"] = "10";
                    context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(_resetTimes[partitionKey]).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return Task.CompletedTask;
            });
        }

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