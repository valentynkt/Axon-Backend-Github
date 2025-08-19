using BuildingBlocks.Core.Abstractions.Time;

namespace BuildingBlocks.Infrastructure.Time;

/// <summary>
/// System implementation of IClock that returns the current system UTC time.
/// This is the default implementation used in production.
/// Implemented as a singleton to ensure consistent behavior across the application.
/// </summary>
public sealed class SystemClock : IClock
{
    private static readonly Lazy<SystemClock> _instance = new(() => new SystemClock());
    
    /// <summary>
    /// Gets the singleton instance of SystemClock.
    /// </summary>
    public static SystemClock Instance => _instance.Value;

    private SystemClock() { }

    /// <summary>
    /// Gets the current system UTC time.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}