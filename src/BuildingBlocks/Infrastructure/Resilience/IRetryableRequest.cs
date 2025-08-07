namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Marker interface for requests that support retry logic.
/// </summary>
public interface IRetryableRequest
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    int MaxRetries { get; }
    
    /// <summary>
    /// Base delay between retry attempts.
    /// </summary>
    TimeSpan RetryDelay { get; }
}