namespace Axon.Modules.Chat.Application.Contracts.Telemetry;

/// <summary>
/// Chat module telemetry for tracking AI requests and message processing performance.
/// </summary>
public interface IChatTelemetry
{
    /// <summary>
    /// Track AI client request metrics including duration and success rate.
    /// </summary>
    void TrackAiRequest(string requestType, TimeSpan duration, bool success);
    
    /// <summary>
    /// Track message processing performance and success rate.
    /// </summary> 
    void TrackMessageProcessed(Guid conversationId, TimeSpan processingTime, bool success);
}