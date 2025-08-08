namespace BuildingBlocks.Core.Events.Replay;

/// <summary>
/// Priority levels for replay operations determining resource allocation and execution order.
/// Created for Epic 06 Story 04 - Event Replay & Recovery Service.
/// Higher priority operations receive more system resources and are processed before lower priority ones.
/// </summary>
public enum ReplayPriority
{
    /// <summary>
    /// Lowest priority for background maintenance operations.
    /// Limited resource allocation, processed when system has spare capacity.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Standard priority for routine replay operations.
    /// Balanced resource allocation suitable for most replay scenarios.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Higher priority for important operational replays.
    /// Increased resource allocation and faster processing.
    /// </summary>
    High = 2,

    /// <summary>
    /// Highest priority for critical recovery operations.
    /// Maximum resource allocation and immediate processing.
    /// Should be used sparingly for genuine emergencies only.
    /// </summary>
    Critical = 3
}