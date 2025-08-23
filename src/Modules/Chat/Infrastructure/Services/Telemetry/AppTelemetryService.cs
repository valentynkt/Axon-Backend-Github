using System.Diagnostics;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Services.Telemetry;

/// <summary>
/// Simple telemetry service implementation for Chat module
/// </summary>
public sealed class AppTelemetryService : IAppTelemetry
{
    private readonly ILogger<AppTelemetryService> _logger;
    private static readonly ActivitySource ActivitySource = new("Axon.Chat");

    public AppTelemetryService(ILogger<AppTelemetryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void TrackEvent(string eventName, Dictionary<string, object>? properties = null)
    {
        using var activity = ActivitySource.StartActivity($"Event.{eventName}");
        
        if (properties != null)
        {
            foreach (var (key, value) in properties)
            {
                activity?.SetTag(key, value.ToString());
            }
        }

        _logger.LogInformation("Event tracked: {EventName} with {PropertyCount} properties", 
            eventName, properties?.Count ?? 0);
    }

    public void TrackMetric(string metricName, double value, Dictionary<string, object>? properties = null)
    {
        using var activity = ActivitySource.StartActivity($"Metric.{metricName}");
        activity?.SetTag("metric.value", value);
        
        if (properties != null)
        {
            foreach (var (key, property) in properties)
            {
                activity?.SetTag(key, property.ToString());
            }
        }

        _logger.LogDebug("Metric tracked: {MetricName} = {Value} with {PropertyCount} properties", 
            metricName, value, properties?.Count ?? 0);
    }

    public void TrackException(Exception exception, Dictionary<string, object>? properties = null)
    {
        using var activity = ActivitySource.StartActivity("Exception");
        activity?.SetTag("exception.type", exception.GetType().Name);
        activity?.SetTag("exception.message", exception.Message);
        
        if (properties != null)
        {
            foreach (var (key, property) in properties)
            {
                activity?.SetTag(key, property.ToString());
            }
        }

        _logger.LogError(exception, "Exception tracked with {PropertyCount} properties", 
            properties?.Count ?? 0);
    }

    public static Activity? StartActivity(string activityName)
    {
        return ActivitySource.StartActivity(activityName);
    }

    public void TrackConversationStarted(Guid conversationId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public void TrackMessageProcessed(Guid conversationId, TimeSpan processingTime, bool success)
    {
        throw new NotImplementedException();
    }

    public void TrackAiClientRequest(string requestType, TimeSpan duration, bool success)
    {
        throw new NotImplementedException();
    }

    public void TrackValidationFailure(string commandType, string errorCode)
    {
        throw new NotImplementedException();
    }
}