namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Defines how events should be ordered and processed during replay operations.
/// Created for Epic 06 Story 04 - Event Replay and Recovery Service.
/// Provides different replay strategies for various operational scenarios.
/// </summary>
public enum ReplayMode
{
    /// <summary>
    /// Events are replayed in chronological order (by OccurredOn timestamp).
    /// Ensures temporal consistency and maintains causality relationships.
    /// This is the most common and safest replay mode.
    /// </summary>
    Chronological = 0,

    /// <summary>
    /// Events are replayed in reverse chronological order (newest first).
    /// Useful for undoing recent changes or analyzing recent system behavior.
    /// </summary>
    ReverseChronological = 1,

    /// <summary>
    /// Events are replayed grouped by aggregate ID in chronological order within each aggregate.
    /// Maintains aggregate consistency while allowing parallel processing of different aggregates.
    /// Optimal for scenarios where aggregate-level consistency is critical.
    /// </summary>
    ByAggregate = 2,

    /// <summary>
    /// Events are replayed grouped by event type in chronological order within each type.
    /// Useful for type-specific processing or when certain event types have dependencies.
    /// </summary>
    ByEventType = 3,

    /// <summary>
    /// Events are replayed grouped by stream name in chronological order within each stream.
    /// Maintains stream-level consistency for bounded context replay scenarios.
    /// </summary>
    ByStream = 4,

    /// <summary>
    /// Events are replayed in their original storage order (typically by event ID or version).
    /// Fastest replay mode but does not guarantee any particular business logic ordering.
    /// Use only when order is not critical for the replay operation.
    /// </summary>
    StorageOrder = 5,

    /// <summary>
    /// Custom ordering based on specified criteria in the replay request.
    /// Allows for application-specific ordering requirements.
    /// </summary>
    Custom = 6
}