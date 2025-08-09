namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Classifies exceptions as transient (HTTP 5xx/429, socket resets, DNS, etc.).
/// Follows the Single Responsibility Principle by focusing solely on fault classification.
/// </summary>
public interface ITransientFaultDetector
{
    /// <summary>
    /// Determines if an exception represents a transient fault that should be retried.
    /// </summary>
    /// <param name="exception">The exception to evaluate</param>
    /// <returns>True if the exception is transient; otherwise false</returns>
    bool IsTransient(Exception exception);
}