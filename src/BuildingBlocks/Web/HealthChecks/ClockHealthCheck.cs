using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Web.HealthChecks;

/// <summary>
/// Health check that verifies the TimeProvider system is properly functioning.
/// </summary>
public sealed class ClockHealthCheck : IHealthCheck
{
    private readonly TimeProvider _timeProvider;

    public ClockHealthCheck(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify TimeProvider returns a sane UTC time
            var currentTime = _timeProvider.GetUtcNow();
            
            // Sanity check: time should be reasonable (not default value)
            if (currentTime == default)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "TimeProvider returns default DateTimeOffset value"));
            }
            
            // Sanity check: time should be UTC
            if (currentTime.Offset != TimeSpan.Zero)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"TimeProvider is not returning UTC time. Offset: {currentTime.Offset}"));
            }
            
            // TimeProvider is working properly
            var data = new Dictionary<string, object>
            {
                ["current_time"] = currentTime.ToString("O"),
                ["offset"] = currentTime.Offset.ToString(),
                ["time_provider_type"] = _timeProvider.GetType().Name
            };
            
            return Task.FromResult(HealthCheckResult.Healthy(
                "TimeProvider system is operational", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "TimeProvider system is not functioning properly", ex));
        }
    }
}