namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error severity levels for alerting, logging, and monitoring.
/// Each level includes alerting thresholds and escalation policies.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational errors - no action required
    /// Use for: User notifications, informational messages
    /// Alerting: No alerts
    /// Logging: Information level
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// Warning errors - attention may be required
    /// Use for: Deprecated API usage, performance warnings, recoverable errors
    /// Alerting: Low priority alerts after threshold
    /// Logging: Warning level
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Error condition - action required
    /// Use for: Business rule violations, validation errors, user errors
    /// Alerting: Medium priority alerts
    /// Logging: Error level
    /// </summary>
    Error = 3,
    
    /// <summary>
    /// Critical system errors - immediate attention required
    /// Use for: Service failures, data corruption, security breaches
    /// Alerting: High priority alerts, immediate escalation
    /// Logging: Error level with extra context
    /// </summary>
    Critical = 4,
    
    /// <summary>
    /// Fatal system errors - system instability
    /// Use for: System crashes, unrecoverable errors, data loss
    /// Alerting: Immediate escalation to on-call team
    /// Logging: Fatal level with full context dump
    /// </summary>
    Fatal = 5
}