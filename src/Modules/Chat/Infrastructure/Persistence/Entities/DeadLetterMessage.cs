namespace Axon.Modules.Chat.Infrastructure.Persistence.Entities;

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