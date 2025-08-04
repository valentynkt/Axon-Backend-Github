namespace Axon.Modules.Chat.Infrastructure.Persistence.Entities;

/// <summary>
/// Outbox message entity for reliable event publishing with transactional guarantees
/// Implements FOR UPDATE SKIP LOCKED processing for concurrent worker safety
/// Following SPARC Event Sourcing architecture patterns
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Fully qualified event type name for deserialization
    /// </summary>
    public string Type { get; init; } = default!;
    
    /// <summary>
    /// Serialized event payload using System.Text.Json
    /// </summary>
    public string Payload { get; init; } = default!;
    
    /// <summary>
    /// Event metadata containing correlation, causation, and context information
    /// </summary>
    public string Metadata { get; init; } = default!;
    
    /// <summary>
    /// Timestamp when the domain event occurred (UTC)
    /// </summary>
    public DateTime OccurredAtUtc { get; init; }
    
    /// <summary>
    /// Timestamp when the message was successfully processed (UTC)
    /// NULL indicates unprocessed message
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }
    
    /// <summary>
    /// Number of processing attempts for retry logic
    /// </summary>
    public int ProcessingAttempts { get; set; }
    
    /// <summary>
    /// Last error message if processing failed
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Next retry attempt timestamp for exponential backoff
    /// </summary>
    public DateTime? NextRetryAtUtc { get; set; }

    /// <summary>
    /// Factory method for creating outbox messages following SPARC patterns
    /// </summary>
    public static OutboxMessage Create(
        string type,
        string payload,
        string metadata,
        DateTime occurredAtUtc) =>
        new()
        {
            Type = type,
            Payload = payload,
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc,
            ProcessingAttempts = 0
        };
}

/// <summary>
/// Dead letter message entity for failed event processing
/// Part of SPARC error handling and resilience patterns
/// </summary>
public sealed class DeadLetterMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid OriginalMessageId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public required string Metadata { get; init; }
    public required string FailureReason { get; init; }
    public required string FailureStackTrace { get; init; }
    public int ProcessingAttempts { get; init; }
    public DateTime OriginalOccurredAtUtc { get; init; }
    public DateTime MovedToDeadLetterAtUtc { get; init; }
    public DateTime? ReprocessedAtUtc { get; set; }
    public bool IsReprocessed { get; set; }
}

/// <summary>
/// Event correlation entity for tracking causation chains
/// Enables distributed tracing and debugging per SPARC patterns
/// </summary>
public sealed class EventCorrelation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string CorrelationId { get; init; }
    public required string EventType { get; init; }
    public required string AggregateId { get; init; }
    public string? CausationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public required string MachineName { get; init; }
}

/// <summary>
/// Conversation read model optimized for queries with full-text search
/// Denormalized for maximum query performance per SPARC CQRS patterns
/// </summary>
public sealed class ConversationReadModel
{
    public Guid Id { get; init; }
    public string Title { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Active";
    public int MessageCount { get; private set; }
    public int ToolExecutionCount { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    
    /// <summary>
    /// JSON context data for flexible querying
    /// </summary>
    public string Context { get; private set; } = "{}";
    
    /// <summary>
    /// JSON array of tags for categorization
    /// </summary>
    public string Tags { get; private set; } = "[]";
    
    /// <summary>
    /// Full-text search vector (computed column in PostgreSQL)
    /// </summary>
    public string SearchVector { get; private set; } = string.Empty;
    
    /// <summary>
    /// Denormalized participant information for efficient querying
    /// </summary>
    public string Participants { get; private set; } = "[]";
    
    /// <summary>
    /// Version for optimistic concurrency control in read models
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// Timestamp of the last event that updated this read model
    /// </summary>
    public DateTime LastEventTimestamp { get; private set; }

    /// <summary>
    /// ID of the last event that was processed for this read model
    /// </summary>
    public string? LastProcessedEventId { get; private set; }

    // Audit fields (backing fields pattern)
    private DateTime _createdAtUtc;
    private string _createdBy = default!;
    private DateTime _updatedAtUtc;
    private string _updatedBy = default!;

    // Public audit properties
    public DateTime CreatedAtUtc => _createdAtUtc;
    public string CreatedBy => _createdBy;
    public DateTime UpdatedAtUtc => _updatedAtUtc;
    public string UpdatedBy => _updatedBy;

    /// <summary>
    /// Internal audit setters for infrastructure layer
    /// </summary>
    internal void SetCreated(DateTime atUtc, string by)
    {
        _createdAtUtc = atUtc;
        _createdBy = by;
    }

    internal void SetUpdated(DateTime atUtc, string by)
    {
        _updatedAtUtc = atUtc;
        _updatedBy = by;
    }

    /// <summary>
    /// Factory method for creating new read model following SPARC patterns
    /// </summary>
    public static ConversationReadModel Create(
        Guid id,
        string title,
        string userId,
        DateTime createdAt)
    {
        var readModel = new ConversationReadModel
        {
            Id = id,
            Title = title,
            Status = "Active",
            IsActive = true,
            LastMessageAt = createdAt,
            MessageCount = 0,
            ToolExecutionCount = 0,
            Participants = System.Text.Json.JsonSerializer.Serialize(new[] { userId })
        };

        readModel.SetCreated(createdAt, userId);
        readModel.SetUpdated(createdAt, userId);
        return readModel;
    }

    /// <summary>
    /// Clock-safe idempotency mechanism per SPARC patterns
    /// </summary>
    public void UpdateVersion(long newVersion, DateTime eventTimestamp, string eventId)
    {
        // Skip if EventId already processed (handles clock skew)
        if (LastProcessedEventId == eventId)
            return; 
            
        if (newVersion <= Version)
            return; // Ignore older events (idempotency)

        Version = newVersion;
        LastEventTimestamp = eventTimestamp;
        LastProcessedEventId = eventId;
    }
}