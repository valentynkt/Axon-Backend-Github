namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Contract for internal commands that remain within a bounded context.
/// These represent actions that should be performed as a result of domain events,
/// but stay within the same bounded context. Transport neutral.
/// </summary>
public interface IInternalCommand : IEvent
{
    /// <summary>
    /// Priority for command processing.
    /// Higher values indicate higher priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Maximum number of retry attempts if command processing fails.
    /// Default should be reasonable for the command type.
    /// </summary>
    int MaxRetryAttempts { get; }

    /// <summary>
    /// Delay between retry attempts in case of failure.
    /// Should use exponential backoff for resilience.
    /// </summary>
    TimeSpan RetryDelay { get; }

    /// <summary>
    /// Optional correlation ID for tracking related commands and events.
    /// Useful for debugging and distributed tracing within the bounded context.
    /// </summary>
    string? CorrelationId { get; }
}