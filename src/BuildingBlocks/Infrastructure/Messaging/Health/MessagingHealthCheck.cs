using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Infrastructure.Messaging.Health;

/// <summary>
/// Non-invasive health check for MassTransit messaging infrastructure.
/// Verifies bus readiness without creating any queues or topics.
/// </summary>
public class MessagingHealthCheck : IHealthCheck
{
    private readonly IBus _bus;
    private readonly IHostedService? _busHostedService;

    public MessagingHealthCheck(IBus bus, IEnumerable<IHostedService> hostedServices)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        // Find the MassTransit hosted service if available
        _busHostedService = hostedServices?.FirstOrDefault(s => s.GetType().Name.Contains("MassTransit"));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we have an IBusControl for more detailed status
            if (_bus is IBusControl busControl)
            {
                // Check the bus state non-invasively
                // Note: We're not starting or creating anything, just checking state
                var busType = busControl.GetType().Name;
                var isStarted = busControl.GetProbeResult().ToString()?.Contains("started") ?? false;
                
                if (isStarted || _busHostedService != null)
                {
                    return Task.FromResult(HealthCheckResult.Healthy(
                        "MassTransit messaging is operational",
                        data: new Dictionary<string, object>
                        {
                            ["status"] = "ready",
                            ["busType"] = busType,
                            ["hostedService"] = _busHostedService != null
                        }));
                }
                
                return Task.FromResult(HealthCheckResult.Degraded(
                    "MassTransit bus is not fully started",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "starting",
                        ["busType"] = busType
                    }));
            }
            
            // Fallback: if we have a bus instance and hosted service, assume healthy
            if (_busHostedService != null)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    "MassTransit messaging service is running",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "ready",
                        ["busType"] = _bus.GetType().Name
                    }));
            }
            
            // Unable to determine status definitively
            return Task.FromResult(HealthCheckResult.Degraded(
                "MassTransit bus status unknown",
                data: new Dictionary<string, object>
                {
                    ["status"] = "unknown",
                    ["busType"] = _bus.GetType().Name
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Failed to check MassTransit health",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }));
        }
    }
}