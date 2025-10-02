using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Extensions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Axon.Api.Modules.Identity.Processors;

/// <summary>
/// Pre-processor for Identity module endpoints that enforces authentication and validates JWT expiration
/// Supports FakeTimeProvider for time-travel testing
/// </summary>
public sealed class IdentityAuthProcessor<TRequest> : IPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<IdentityAuthProcessor<TRequest>> _logger;
    private readonly TimeProvider _timeProvider;

    public IdentityAuthProcessor(
        ILogger<IdentityAuthProcessor<TRequest>> logger,
        TimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        // Enforce authentication at endpoint level (defense-in-depth with FallbackPolicy)
        // Required because FastEndpoints may bypass middleware authorization when Policies() is used
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            await httpContext.SendProblemDetailsAsync(
                Error.Unauthorized("Authentication required", "AUTH.UNAUTHENTICATED"),
                ct);

            // Stop pipeline execution
            await context.HttpContext.Response.StartAsync(ct);
            return;
        }

        // Additional validation: Check token expiration manually using application TimeProvider
        // This is necessary because JWT middleware may not honor FakeTimeProvider in test environments
        // In production, this provides defense-in-depth alongside middleware validation
        var expClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var expUnix))
        {
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            var now = _timeProvider.GetUtcNow();

            if (expDate < now)
            {
                _logger.LogWarning(
                    "Token expired: exp={ExpDate}, now={Now}, traceId={TraceId}",
                    expDate, now, httpContext.TraceIdentifier);

                await httpContext.SendProblemDetailsAsync(
                    Error.Unauthorized("Token has expired", "AUTH.TOKEN_EXPIRED"),
                    ct);

                // Stop pipeline execution
                await httpContext.Response.StartAsync(ct);
                return;
            }
        }
    }
}
