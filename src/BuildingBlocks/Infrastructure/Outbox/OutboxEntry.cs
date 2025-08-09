using BuildingBlocks.Core.Domain.Primitives;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Entity representing an outbox entry for reliable event publishing.
/// Stores domain events within the same database transaction as business data
/// to ensure atomicity and enable reliable event processing.
/// </summary>
public sealed class OutboxEntry : Entity<OutboxEntryId>
{
    /// <summary>
    /// Default constructor for EF Core
    /// </summary>
    private OutboxEntry() : base()
    {
    }

    /// <summary>
    /// Create a new outbox entry
    /// </summary>
    public OutboxEntry(
        OutboxEntryId id,
        Guid transactionId,
        string eventType,
        string eventData,
        string? traceId = null,
        Guid? requestId = null,
        string? tenantId = null,
        string? metadata = null) : base(id)
    {
        TransactionId = transactionId;
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        EventData = eventData ?? throw new ArgumentNullException(nameof(eventData));
        TraceId = traceId;
        RequestId = requestId;
        TenantId = tenantId;
        CreatedAt = DateTime.UtcNow;
        Status = OutboxEntryStatus.Pending;
        RetryCount = 0;
        Metadata = metadata;
    }

    /// <summary>
    /// Transaction ID that created this outbox entry
    /// </summary>
    public Guid TransactionId { get; private set; }

    /// <summary>
    /// Assembly qualified name of the event type for deserialization
    /// </summary>
    [MaxLength(500)]
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized event data (JSON)
    /// </summary>
    public string EventData { get; private set; } = string.Empty;

    /// <summary>
    /// W3C Trace ID for correlation across distributed services
    /// </summary>
    [MaxLength(32)]
    public string? TraceId { get; private set; }

    /// <summary>
    /// Request ID that generated this event
    /// </summary>
    public Guid? RequestId { get; private set; }

    /// <summary>
    /// Tenant ID for multi-tenant scenarios
    /// </summary>
    [MaxLength(100)]
    public string? TenantId { get; private set; }

    /// <summary>
    /// When this entry was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When processing started (for timeout detection)
    /// </summary>
    public DateTime? ProcessingStartedAt { get; private set; }

    /// <summary>
    /// When this entry was successfully processed
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Current processing status
    /// </summary>
    public OutboxEntryStatus Status { get; private set; }

    /// <summary>
    /// Number of processing attempts
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Last error message if processing failed
    /// </summary>
    [MaxLength(2000)]
    public string? LastError { get; private set; }

    /// <summary>
    /// When the next retry attempt should occur (exponential backoff)
    /// </summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>
    /// Additional metadata as JSON (request context, etc.)
    /// </summary>
    public string? Metadata { get; private set; }

    /// <summary>
    /// Version for optimistic concurrency control
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// Mark entry as being processed
    /// </summary>
    public void MarkAsProcessing()
    {
        if (Status == OutboxEntryStatus.Completed || Status == OutboxEntryStatus.DeadLetter)
        {
            throw new InvalidOperationException(
                $"Cannot mark entry {Id} as processing when status is {Status}");
        }

        Status = OutboxEntryStatus.Processing;
        ProcessingStartedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Mark entry as successfully completed
    /// </summary>
    public void MarkAsCompleted()
    {
        if (Status != OutboxEntryStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Cannot mark entry {Id} as completed when status is {Status}. Must be Processing.");
        }

        Status = OutboxEntryStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
        LastError = null; // Clear any previous error
        NextRetryAt = null;
        Version++;
    }

    /// <summary>
    /// Mark entry as failed and increment retry count with exponential backoff
    /// </summary>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="baseDelayMinutes">Base delay in minutes for exponential backoff</param>
    public void MarkAsFailed(string errorMessage, int baseDelayMinutes = 1)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        Status = OutboxEntryStatus.Failed;
        RetryCount++;
        LastError = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        ProcessingStartedAt = null; // Reset processing timestamp
        
        // Exponential backoff: baseDelay * 2^(retryCount-1)
        var delayMinutes = baseDelayMinutes * Math.Pow(2, RetryCount - 1);
        NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        Version++;
    }

    /// <summary>
    /// Move entry to dead letter queue when max retries exceeded
    /// </summary>
    /// <param name="finalErrorMessage">Final error message</param>
    public void MoveToDeadLetter(string? finalErrorMessage = null)
    {
        Status = OutboxEntryStatus.DeadLetter;
        ProcessedAt = DateTime.UtcNow;
        ProcessingStartedAt = null;
        NextRetryAt = null;
        
        if (!string.IsNullOrWhiteSpace(finalErrorMessage))
        {
            LastError = finalErrorMessage.Length > 2000 ? finalErrorMessage[..2000] : finalErrorMessage;
        }
        
        Version++;
    }

    /// <summary>
    /// Reset entry to pending status for manual retry
    /// </summary>
    public void ResetToPending()
    {
        if (Status == OutboxEntryStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Cannot reset completed entry {Id} to pending");
        }

        Status = OutboxEntryStatus.Pending;
        ProcessingStartedAt = null;
        ProcessedAt = null;
        NextRetryAt = null;
        LastError = null;
        // Don't reset RetryCount to preserve history
        Version++;
    }

    /// <summary>
    /// Check if entry is ready for processing (not being processed and retry time passed)
    /// </summary>
    /// <param name="processingTimeoutMinutes">Minutes after which a processing entry is considered stale</param>
    /// <returns>True if ready for processing</returns>
    public bool IsReadyForProcessing(int processingTimeoutMinutes = 30)
    {
        var now = DateTime.UtcNow;

        return Status switch
        {
            OutboxEntryStatus.Pending => true,
            OutboxEntryStatus.Processing => ProcessingStartedAt.HasValue && 
                                          now > ProcessingStartedAt.Value.AddMinutes(processingTimeoutMinutes),
            OutboxEntryStatus.Failed => !NextRetryAt.HasValue || now >= NextRetryAt.Value,
            _ => false
        };
    }

    /// <summary>
    /// Get processing delay information for monitoring/debugging
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
/// Strong ID for outbox entries
/// </summary>
public sealed record OutboxEntryId(Guid Value) : IStrongId
{
    public static OutboxEntryId New() => new(Guid.NewGuid());
    public static OutboxEntryId From(Guid value) => new(value);
    
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Status enumeration for outbox entries
/// </summary>
public enum OutboxEntryStatus
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