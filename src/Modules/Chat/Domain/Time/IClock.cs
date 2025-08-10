namespace Axon.Modules.Chat.Domain.Time;

/// <summary>
/// Abstraction for getting the current UTC time in a deterministic and testable way.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}