namespace Axon.Modules.Chat.Domain.Time;

/// <summary>
/// System implementation of IClock that returns the current system UTC time.
/// This is the default implementation used in production.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Gets the current system UTC time.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}