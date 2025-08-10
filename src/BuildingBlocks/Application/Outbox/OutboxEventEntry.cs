using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Application layer representation of an outbox entry.
/// This DTO decouples the Application layer from Infrastructure concerns.
/// </summary>
public sealed record OutboxEventEntry(
    Guid Id,
    Guid TransactionId,
    string EventType,
    string EventData,
    OutboxEventStatus Status,
    int RetryCount,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt = null,
    DateTime? ProcessedAt = null,
    string? LastError = null,
    DateTime? NextRetryAt = null,
    string? TraceId = null,
    Guid? RequestId = null,
    string? TenantId = null,
    string? Metadata = null)
{
    /// <summary>
    /// Deserialize the event data back to a domain event.
    /// </summary>
    /// <param name="eventType">The type to deserialize to</param>
    /// <returns>The deserialized domain event, or null if failed</returns>
    public IDomainEvent? DeserializeEvent(Type eventType)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(EventData, eventType) as IDomainEvent;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if this entry is ready for processing based on current status and retry timing.
    /// </summary>
    /// <param name="processingTimeoutMinutes">Minutes after which a processing entry is considered stale</param>
    /// <returns>True if ready for processing</returns>
    public bool IsReadyForProcessing(int processingTimeoutMinutes = 30)
    {
        var now = DateTime.UtcNow;

        return Status switch
        {
            OutboxEventStatus.Pending => true,
            OutboxEventStatus.Processing => ProcessingStartedAt.HasValue && 
                                          now > ProcessingStartedAt.Value.AddMinutes(processingTimeoutMinutes),
            OutboxEventStatus.Failed => !NextRetryAt.HasValue || now >= NextRetryAt.Value,
            _ => false
        };
    }

    /// <summary>
    /// Get processing delay information for monitoring/debugging.
    /// </summary>
    public (TimeSpan Age, TimeSpan? TimeUntilRetry) GetProcessingInfo()
    {
        var now = DateTime.UtcNow;
        var age = now - CreatedAt;
        
        TimeSpan? timeUntilRetry = null;
        if (NextRetryAt.HasValue && now < NextRetryAt.Value)
        {
            timeUntilRetry = NextRetryAt.Value - now;
        }

        return (age, timeUntilRetry);
    }
}

/// <summary>
/// Status enumeration for outbox events in the Application layer.
/// </summary>
public enum OutboxEventStatus
{
    /// <summary>
    /// Entry is waiting to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Entry is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Entry was successfully processed and published
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Entry failed processing and is waiting for retry
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Entry failed max retries and moved to dead letter queue
    /// </summary>
    DeadLetter = 4
}

/// <summary>
/// Information about a failed outbox processing attempt.
/// </summary>
public sealed record OutboxFailureInfo(
    Guid EntryId,
    string ErrorMessage,
    DateTime FailedAt,
    DateTime? NextRetryAtUtc = null,
    bool MoveToDeadLetter = false);