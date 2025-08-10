using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Application-layer DTO representing an outbox entry.
/// Infrastructure maps its persistence entity to/from this shape.
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
    /// <summary>Best-effort deserialization back to a domain event (avoid on hot paths).</summary>
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

    /// <summary>Whether this entry is ready to be processed right now.</summary>
    public bool IsReadyForProcessing(int processingTimeoutMinutes = 30)
    {
        var now = DateTime.UtcNow;

        return Status switch
        {
            OutboxEventStatus.Pending => true,
            OutboxEventStatus.Processing => ProcessingStartedAt is not null &&
                                            now > ProcessingStartedAt.Value.AddMinutes(processingTimeoutMinutes),
            OutboxEventStatus.Failed => !NextRetryAt.HasValue || now >= NextRetryAt.Value,
            _ => false
        };
    }

    /// <summary>Useful timing details for logs/metrics.</summary>
    public (TimeSpan Age, TimeSpan? TimeUntilRetry) GetProcessingInfo()
    {
        var now = DateTime.UtcNow;
        var age = now - CreatedAt;

        TimeSpan? until = null;
        if (NextRetryAt.HasValue && now < NextRetryAt.Value)
            until = NextRetryAt.Value - now;

        return (age, until);
    }
}
