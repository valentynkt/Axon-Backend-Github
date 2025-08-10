namespace BuildingBlocks.Application.Events.Consumption.Inbox;

/// <summary>
/// Result of attempting to begin processing an integration event through the inbox.
/// </summary>
public enum InboxStartResult
{
    /// <summary>
    /// Processing can begin - this is the first time we've seen this idempotency key.
    /// </summary>
    Begun,
    
    /// <summary>
    /// Event has already been successfully processed - skip processing.
    /// </summary>
    AlreadyCompleted,
    
    /// <summary>
    /// Event is currently being processed by another instance - skip processing.
    /// </summary>
    InFlight
}