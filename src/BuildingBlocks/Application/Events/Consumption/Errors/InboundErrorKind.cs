namespace BuildingBlocks.Application.Events.Consumption.Errors;

/// <summary>
/// Classification of integration event handler failures for retry/dead-letter decisions.
/// Used to determine appropriate handling strategy for failed message processing.
/// </summary>
public enum InboundErrorKind
{
    /// <summary>
    /// Transient error that may succeed on retry.
    /// Examples: timeout, network failure, temporary database unavailability.
    /// </summary>
    Transient,
    
    /// <summary>
    /// Permanent error that will not succeed on retry.
    /// Examples: validation failure, malformed data, business rule violation.
    /// </summary>
    Permanent,
    
    /// <summary>
    /// Unclassified error where the handling strategy is configuration-dependent.
    /// Fallback classification when error type cannot be definitively categorized.
    /// </summary>
    Unclassified
}