using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using BuildingBlocks.Infrastructure.Observability.HealthChecks;
using BuildingBlocks.Infrastructure.Persistence.Postgres;
using BuildingBlocks.Web.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace BuildingBlocks.Web.Extensions;

public static class HealthCheckExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services)
    {
        // Add basic health checks for production monitoring
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck("memory", () =>
            {
                var memoryUsed = GC.GetTotalMemory(false);
                var memoryUsedMb = memoryUsed / 1024 / 1024;
                
                if (memoryUsedMb > 1024) // 1GB threshold
                {
                    return HealthCheckResult.Degraded($"Memory usage is {memoryUsedMb} MB");
                }
                
                return HealthCheckResult.Healthy($"Memory usage is {memoryUsedMb} MB");
            });

        return services;
    }

    public static WebApplication UseCustomHealthCheck(this WebApplication app)
    {
        // Map health check endpoints
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
        });

        return app;
    }

    /// <summary>
    /// Adds a basic clock health check to verify system time.
    /// </summary>
    /// <param name="builder">The health checks builder</param>
    /// <param name="name">The health check name (defaults to "clock")</param>
    /// <param name="tags">Optional tags for the health check</param>
    /// <returns>The health checks builder for chaining</returns>
    public static IHealthChecksBuilder AddClockHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "clock",
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, () =>
        {
            var now = DateTime.UtcNow;
            // Simple validation that system time is reasonable (within last 10 years)
            if (now.Year >= 2014)
            {
                return HealthCheckResult.Healthy($"System time: {now:yyyy-MM-dd HH:mm:ss} UTC");
            }
            return HealthCheckResult.Unhealthy("System clock appears to be incorrect");
        }, tags?.ToArray() ?? []);
    }
}