using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Centralized outbox message entity for reliable event publishing with transactional guarantees.
/// Moved from Chat module to BuildingBlocks for Epic 06 Story 01 - Centralized Outbox Processor Service.
/// Implements FOR UPDATE SKIP LOCKED processing for concurrent worker safety.
/// Enhanced with Result pattern integration and SPARC Event Sourcing architecture patterns.
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
    /// Factory method for creating outbox messages following SPARC patterns with Result pattern validation
    /// </summary>
    public static Result<OutboxMessage> Create(
        string type,
        string payload,
        string metadata,
        DateTime occurredAtUtc)
    {
        // Validate input parameters using Result pattern
        if (string.IsNullOrWhiteSpace(type))
            return Result<OutboxMessage>.Failure(Error.Validation(
                "Event type cannot be null or empty", 
                "OUTBOX_MESSAGE_INVALID_TYPE"));

        if (string.IsNullOrWhiteSpace(payload))
            return Result<OutboxMessage>.Failure(Error.Validation(
                "Event payload cannot be null or empty", 
                "OUTBOX_MESSAGE_INVALID_PAYLOAD"));

        if (string.IsNullOrWhiteSpace(metadata))
            return Result<OutboxMessage>.Failure(Error.Validation(
                "Event metadata cannot be null or empty", 
                "OUTBOX_MESSAGE_INVALID_METADATA"));

        if (occurredAtUtc == default || occurredAtUtc > DateTime.UtcNow.AddMinutes(5))
            return Result<OutboxMessage>.Failure(Error.Validation(
                "Event occurrence time must be valid and not in the future", 
                "OUTBOX_MESSAGE_INVALID_OCCURRED_TIME"));

        var outboxMessage = new OutboxMessage
        {
            Type = type,
            Payload = payload,
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc,
            ProcessingAttempts = 0
        };

        return Result<OutboxMessage>.Success(outboxMessage);
    }

    /// <summary>
    /// Legacy factory method for backward compatibility (without Result pattern)
    /// </summary>
    [Obsolete("Use Create method with Result pattern instead. This method will be removed in future versions.")]
    public static OutboxMessage CreateLegacy(
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

    /// <summary>
    /// Mark message as processing started following Result pattern
    /// </summary>
    public Result<Unit> MarkAsProcessing()
    {
        if (ProcessedAtUtc.HasValue)
            return Result<Unit>.Failure(Error.BusinessRule(
                "Cannot mark already processed message as processing", 
                "OUTBOX_MESSAGE_ALREADY_PROCESSED"));

        ProcessingAttempts++;
        return Result<Unit>.Success();
    }

    /// <summary>
    /// Mark message as successfully processed following Result pattern
    /// </summary>
    public Result<Unit> MarkAsProcessed()
    {
        if (ProcessedAtUtc.HasValue)
            return Result<Unit>.Failure(Error.BusinessRule(
                "Message is already marked as processed", 
                "OUTBOX_MESSAGE_ALREADY_PROCESSED"));

        ProcessedAtUtc = DateTime.UtcNow;
        LastError = null; // Clear any previous error
        NextRetryAtUtc = null; // Clear retry schedule
        
        return Result<Unit>.Success();
    }

    /// <summary>
    /// Mark message as failed with exponential backoff retry logic following Result pattern
    /// </summary>
    public Result<Unit> MarkAsFailed(string errorMessage, int baseRetryDelayMinutes = 1)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return Result<Unit>.Failure(Error.Validation(
                "Error message cannot be null or empty", 
                "OUTBOX_MESSAGE_INVALID_ERROR"));

        if (ProcessedAtUtc.HasValue)
            return Result<Unit>.Failure(Error.BusinessRule(
                "Cannot mark already processed message as failed", 
                "OUTBOX_MESSAGE_ALREADY_PROCESSED"));

        LastError = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        
        // Exponential backoff: baseDelay * 2^(attempts-1)
        var delayMinutes = baseRetryDelayMinutes * Math.Pow(2, ProcessingAttempts - 1);
        NextRetryAtUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
        
        return Result<Unit>.Success();
    }

    /// <summary>
    /// Check if message is ready for processing (considers retry timing and processing status)
    /// </summary>
    public bool IsReadyForProcessing()
    {
        // Already processed messages are not ready for processing
        if (ProcessedAtUtc.HasValue)
            return false;

        // If no retry schedule, message is ready
        if (!NextRetryAtUtc.HasValue)
            return true;

        // Check if retry time has passed
        return DateTime.UtcNow >= NextRetryAtUtc.Value;
    }

    /// <summary>
    /// Get processing metadata for monitoring and diagnostics
    /// </summary>
    public OutboxMessageMetadata GetProcessingMetadata()
    {
        var age = DateTime.UtcNow - OccurredAtUtc;
        TimeSpan? timeUntilRetry = null;
        
        if (NextRetryAtUtc.HasValue && DateTime.UtcNow < NextRetryAtUtc.Value)
        {
            timeUntilRetry = NextRetryAtUtc.Value - DateTime.UtcNow;
        }

        return new OutboxMessageMetadata(
            Id: Id,
            Age: age,
            ProcessingAttempts: ProcessingAttempts,
            IsProcessed: ProcessedAtUtc.HasValue,
            HasError: !string.IsNullOrEmpty(LastError),
            TimeUntilRetry: timeUntilRetry,
            LastError: LastError);
    }
}

/// <summary>
/// Metadata information for outbox message processing and monitoring
/// </summary>
public sealed record OutboxMessageMetadata(
    Guid Id,
    TimeSpan Age,
    int ProcessingAttempts,
    bool IsProcessed,
    bool HasError,
    TimeSpan? TimeUntilRetry,
    string? LastError);