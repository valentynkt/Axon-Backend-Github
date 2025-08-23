namespace Axon.Modules.Chat.Application.Contracts.Telemetry;

/// <summary>
/// Interface for application telemetry and metrics collection
/// </summary>
public interface IAppTelemetry
{
    /// <summary>
    /// Track conversation start event
    /// </summary>
    void TrackConversationStarted(Guid conversationId, Guid userId);
    
    /// <summary>
    /// Track message processing metrics
    /// </summary>
    void TrackMessageProcessed(Guid conversationId, TimeSpan processingTime, bool success);
    
    /// <summary>
    /// Track AI client interaction metrics
    /// </summary>
    void TrackAiClientRequest(string requestType, TimeSpan duration, bool success);
    
    /// <summary>
    /// Track validation failures
    /// </summary>
    void TrackValidationFailure(string commandType, string errorCode);
}