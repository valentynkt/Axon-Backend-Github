namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Status values for replay operations throughout their lifecycle.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Provides comprehensive tracking of operation state transitions.
/// </summary>
public enum ReplayOperationStatus
{
    /// <summary>
    /// Operation has been created but not yet started.
    /// Initial state after replay request validation.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Operation is in the process of starting up.
    /// Performing initial setup, event discovery, and resource allocation.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// Operation is actively processing events.
    /// Events are being replayed according to the specified configuration.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Operation has been temporarily paused.
    /// Can be resumed to continue from current position.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Operation is in the process of stopping.
    /// Completing current batch and performing cleanup operations.
    /// </summary>
    Stopping = 4,

    /// <summary>
    /// Operation completed successfully.
    /// All events were processed according to specification.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Operation was cancelled before completion.
    /// Stopped due to user request or system shutdown.
    /// </summary>
    Cancelled = 6,

    /// <summary>
    /// Operation failed due to an error.
    /// Check ErrorMessage for detailed failure information.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// Operation completed with partial success.
    /// Some events were processed successfully while others failed.
    /// Check metrics for detailed success/failure breakdown.
    /// </summary>
    PartiallyCompleted = 8
}