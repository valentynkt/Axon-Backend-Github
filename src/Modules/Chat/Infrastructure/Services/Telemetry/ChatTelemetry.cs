using System.Diagnostics;
using System.Diagnostics.Metrics;
using Axon.Modules.Chat.Application.Contracts.Telemetry;

namespace Axon.Modules.Chat.Infrastructure.Services.Telemetry;

/// <summary>
/// Chat module telemetry implementation using OpenTelemetry metrics.
/// Follows BuildingBlocks pattern similar to ErrorTelemetry.
/// </summary>
public sealed class ChatTelemetry : IChatTelemetry
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat", "1.0");
    private static readonly Meter Meter = new("Axon.Chat", "1.0");
    
    // AI Request metrics
    private static readonly Counter<long> AiRequestsTotal = 
        Meter.CreateCounter<long>("chat.ai_requests_total");
    private static readonly Histogram<double> AiRequestDurationMs = 
        Meter.CreateHistogram<double>("chat.ai_request_duration_ms");
        
    // Message processing metrics
    private static readonly Counter<long> MessagesProcessedTotal = 
        Meter.CreateCounter<long>("chat.messages_processed_total");
    private static readonly Histogram<double> MessageProcessingDurationMs = 
        Meter.CreateHistogram<double>("chat.message_processing_duration_ms");

    public Activity? StartActivity(string name)
    {
        return ActivitySource.StartActivity(name);
    }

    public void TrackAiRequest(string requestType, TimeSpan duration, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("request_type", requestType),
            new("success", success)
        };

        AiRequestsTotal.Add(1, tags);
        AiRequestDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    public void TrackMessageProcessed(Guid conversationId, TimeSpan processingTime, bool success)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("conversation_id", conversationId.ToString()),
            new("success", success)
        };

        MessagesProcessedTotal.Add(1, tags);
        MessageProcessingDurationMs.Record(processingTime.TotalMilliseconds, tags);
    }
}