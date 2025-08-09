using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Health check for the centralized outbox message processor.
/// Created for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Provides comprehensive health monitoring with configurable thresholds and detailed diagnostics.
/// </summary>
public sealed class OutboxProcessorHealthCheck : IHealthCheck
{
    private readonly IOutboxMessageProcessor _outboxProcessor;
    private readonly OutboxProcessorOptions _options;
    private readonly ILogger<OutboxProcessorHealthCheck> _logger;

    public OutboxProcessorHealthCheck(
        IOutboxMessageProcessor outboxProcessor,
        IOptions<OutboxProcessorOptions> options,
        ILogger<OutboxProcessorHealthCheck> logger)
    {
        _outboxProcessor = outboxProcessor ?? throw new ArgumentNullException(nameof(outboxProcessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableHealthChecks)
        {
            return HealthCheckResult.Healthy("Outbox processor health checks are disabled");
        }

        try
        {
            var healthResult = await _outboxProcessor.GetHealthAsync(cancellationToken);

            if (healthResult.IsFailure)
            {
                _logger.LogWarning("Failed to get outbox processor health: {Error}", healthResult.Error.Message);
                return HealthCheckResult.Unhealthy(
                    $"Failed to get outbox processor health: {healthResult.Error.Message}",
                    data: new Dictionary<string, object>
                    {
                        { "error_code", healthResult.Error.Code },
                        { "error_type", healthResult.Error.Type.ToString() }
                    });
            }

            var health = healthResult.Value;
            var data = new Dictionary<string, object>
            {
                { "is_running", health.IsRunning },
                { "last_processing_run", health.LastProcessingRun?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never" },
                { "time_since_last_run", health.TimeSinceLastRun?.ToString(@"hh\:mm\:ss") ?? "N/A" },
                { "pending_message_count", health.PendingMessageCount },
                { "failed_message_count", health.FailedMessageCount },
                { "recent_errors_count", health.RecentErrors.Count }
            };

            // Determine health status based on various factors
            var status = DetermineHealthStatus(health);
            var description = BuildHealthDescription(health, status);

            if (health.RecentErrors.Any())
            {
                data.Add("recent_errors", health.RecentErrors.Take(5).ToArray());
            }

            return new HealthCheckResult(status, description, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while checking outbox processor health");
            return HealthCheckResult.Unhealthy(
                $"Exception occurred while checking outbox processor health: {ex.Message}",
                ex,
                data: new Dictionary<string, object>
                {
                    { "exception_type", ex.GetType().Name },
                    { "stack_trace", ex.StackTrace ?? "N/A" }
                });
        }
    }

    private HealthStatus DetermineHealthStatus(OutboxMessageProcessorHealth health)
    {
        var issues = new List<string>();

        // Check if processor is running
        if (!health.IsRunning)
        {
            issues.Add("Processor is not running");
        }

        // Check if processor has been running recently
        if (health.TimeSinceLastRun.HasValue && health.TimeSinceLastRun.Value > _options.HealthCheckMaxAge)
        {
            issues.Add($"Processor hasn't run for {health.TimeSinceLastRun.Value.TotalMinutes:F1} minutes");
        }

        // Check pending message count
        if (health.PendingMessageCount > _options.BatchSize * 5)
        {
            issues.Add($"High number of pending messages: {health.PendingMessageCount}");
        }

        // Check failed message count
        if (health.FailedMessageCount > _options.HealthCheckMaxFailedMessages)
        {
            issues.Add($"High number of failed messages: {health.FailedMessageCount}");
        }

        // Check for recent errors
        var recentErrors = health.RecentErrors.Where(e => e.Contains(DateTime.UtcNow.ToString("yyyy-MM-dd"))).ToList();
        if (recentErrors.Count > 3)
        {
            issues.Add($"Multiple recent errors: {recentErrors.Count}");
        }

        if (issues.Any())
        {
            // Critical issues that indicate unhealthy state
            if (!health.IsRunning || 
                health.FailedMessageCount > _options.HealthCheckMaxFailedMessages * 2 ||
                (health.TimeSinceLastRun.HasValue && health.TimeSinceLastRun.Value > _options.HealthCheckMaxAge.Add(TimeSpan.FromMinutes(30))))
            {
                return HealthStatus.Unhealthy;
            }

            // Minor issues that indicate degraded state
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    private static string BuildHealthDescription(OutboxMessageProcessorHealth health, HealthStatus status)
    {
        var description = status switch
        {
            HealthStatus.Healthy => "Outbox processor is running normally",
            HealthStatus.Degraded => "Outbox processor is running but has some issues",
            HealthStatus.Unhealthy => "Outbox processor has critical issues",
            _ => "Outbox processor status unknown"
        };

        var details = new List<string>();

        if (!health.IsRunning)
        {
            details.Add("not running");
        }

        if (health.PendingMessageCount > 0)
        {
            details.Add($"{health.PendingMessageCount} pending messages");
        }

        if (health.FailedMessageCount > 0)
        {
            details.Add($"{health.FailedMessageCount} failed messages");
        }

        if (health.TimeSinceLastRun.HasValue)
        {
            details.Add($"last run {health.TimeSinceLastRun.Value.TotalMinutes:F1} minutes ago");
        }

        if (health.RecentErrors.Any())
        {
            details.Add($"{health.RecentErrors.Count} recent errors");
        }

        if (details.Any())
        {
            description += $" ({string.Join(", ", details)})";
        }

        return description;
    }
}

/// <summary>
/// Extension methods for registering outbox processor health checks
/// </summary>
public static class OutboxProcessorHealthCheckExtensions
{
    /// <summary>
    /// Add outbox processor health check to the health check builder
    /// </summary>
    public static IServiceCollection AddOutboxProcessorHealthCheck(
        this IServiceCollection services,
        string name = "outbox_processor",
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return services.AddHealthChecks()
            .AddCheck<OutboxProcessorHealthCheck>(
                name,
                failureStatus,
                tags,
                timeout)
            .Services;
    }
}