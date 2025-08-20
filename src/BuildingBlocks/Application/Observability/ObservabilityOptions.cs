namespace BuildingBlocks.Application.Observability;

/// <summary>
/// Configuration options for observability behavior
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Threshold in milliseconds for logging slow request warnings.
    /// Requests taking longer than this will be logged as warnings.
    /// Default: 2000ms
    /// </summary>
    public int SlowRequestWarningMs { get; init; } = 2000;
}