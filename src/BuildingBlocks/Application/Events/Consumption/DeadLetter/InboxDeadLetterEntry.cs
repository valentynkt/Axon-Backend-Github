using BuildingBlocks.Application.Events.Enveloping;

namespace BuildingBlocks.Application.Events.Consumption.DeadLetter;

/// <summary>
/// Represents a dead-lettered integration event with comprehensive error details.
/// Contains original envelope, error information, and processing history for forensic analysis.
/// </summary>
/// <param name="IdempotencyKey">Unique idempotency key used for deduplication</param>
/// <param name="EventTypeName">Name of the integration event type</param>
/// <param name="Envelope">Complete original integration event envelope</param>
/// <param name="ErrorMessage">Primary error message from the final failure</param>
/// <param name="StackTrace">Stack trace from the final failure (if available)</param>
/// <param name="AttemptCount">Total number of processing attempts before dead-lettering</param>
/// <param name="FirstFailedAt">Timestamp of the first processing failure</param>
/// <param name="DeadLetteredAt">Timestamp when moved to dead letter store</param>
public sealed record InboxDeadLetterEntry(
    string IdempotencyKey,
    string EventTypeName,
    IntegrationEventEnvelope Envelope,
    string ErrorMessage,
    string? StackTrace,
    int AttemptCount,
    DateTime FirstFailedAt,
    DateTime DeadLetteredAt
)
{
    /// <summary>
    /// Creates a dead letter entry from a processing failure.
    /// </summary>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="eventTypeName">Integration event type name</param>
    /// <param name="envelope">Original event envelope</param>
    /// <param name="exception">Final exception that caused dead-lettering</param>
    /// <param name="attemptCount">Total retry attempts</param>
    /// <param name="firstFailedAt">When first failure occurred</param>
    /// <param name="deadLetteredAt">When moved to dead letter (defaults to now)</param>
    /// <returns>Dead letter entry</returns>
    public static InboxDeadLetterEntry FromFailure(
        string idempotencyKey,
        string eventTypeName,
        IntegrationEventEnvelope envelope,
        Exception exception,
        int attemptCount,
        DateTime firstFailedAt,
        DateTime? deadLetteredAt = null)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(eventTypeName);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(exception);

        return new InboxDeadLetterEntry(
            IdempotencyKey: idempotencyKey,
            EventTypeName: eventTypeName,
            Envelope: envelope,
            ErrorMessage: exception.Message,
            StackTrace: exception.StackTrace,
            AttemptCount: attemptCount,
            FirstFailedAt: firstFailedAt,
            DeadLetteredAt: deadLetteredAt ?? DateTime.UtcNow
        );
    }

    /// <summary>
    /// Creates a dead letter entry from a string error message.
    /// </summary>
    /// <param name="idempotencyKey">Unique idempotency key</param>
    /// <param name="eventTypeName">Integration event type name</param>
    /// <param name="envelope">Original event envelope</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="attemptCount">Total retry attempts</param>
    /// <param name="firstFailedAt">When first failure occurred</param>
    /// <param name="deadLetteredAt">When moved to dead letter (defaults to now)</param>
    /// <returns>Dead letter entry</returns>
    public static InboxDeadLetterEntry FromError(
        string idempotencyKey,
        string eventTypeName,
        IntegrationEventEnvelope envelope,
        string errorMessage,
        int attemptCount,
        DateTime firstFailedAt,
        DateTime? deadLetteredAt = null)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(eventTypeName);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return new InboxDeadLetterEntry(
            IdempotencyKey: idempotencyKey,
            EventTypeName: eventTypeName,
            Envelope: envelope,
            ErrorMessage: errorMessage,
            StackTrace: null,
            AttemptCount: attemptCount,
            FirstFailedAt: firstFailedAt,
            DeadLetteredAt: deadLetteredAt ?? DateTime.UtcNow
        );
    }
}