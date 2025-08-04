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

