using Axon.Modules.Chat.Domain.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

/// <summary>
/// Fixed clock implementation for deterministic time-based testing.
/// Returns a constant time value for all operations.
/// </summary>
public class FixedClock : IClock
{
    private readonly DateTimeOffset _fixedTime;

    /// <summary>
    /// Creates a fixed clock with the specified time.
    /// </summary>
    public FixedClock(DateTimeOffset fixedTime)
    {
        _fixedTime = fixedTime;
    }

    /// <summary>
    /// Returns the fixed UTC time.
    /// </summary>
    public DateTimeOffset UtcNow => _fixedTime;

    /// <summary>
    /// Creates a fixed clock for the current UTC time.
    /// </summary>
    public static FixedClock Now() => new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a fixed clock for a specific date and time.
    /// </summary>
    public static FixedClock At(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new FixedClock(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
    }

    /// <summary>
    /// Creates a fixed clock at the Unix epoch (1970-01-01 00:00:00 UTC).
    /// </summary>
    public static FixedClock Epoch() => new(DateTimeOffset.UnixEpoch);

    /// <summary>
    /// Creates a fixed clock at the start of today (UTC).
    /// </summary>
    public static FixedClock TodayStart()
    {
        var now = DateTimeOffset.UtcNow;
        return new FixedClock(new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Advances the clock by the specified time span, returning a new instance.
    /// </summary>
    public FixedClock Advance(TimeSpan timeSpan)
    {
        return new FixedClock(_fixedTime.Add(timeSpan));
    }

    /// <summary>
    /// Rewinds the clock by the specified time span, returning a new instance.
    /// </summary>
    public FixedClock Rewind(TimeSpan timeSpan)
    {
        return new FixedClock(_fixedTime.Subtract(timeSpan));
    }

    public override string ToString()
    {
        return $"FixedClock: {_fixedTime:yyyy-MM-dd HH:mm:ss.fff} UTC";
    }
}