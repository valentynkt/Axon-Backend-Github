namespace BuildingBlocks.Core.Abstractions.Time;

/// <summary>
/// Static accessor for the current clock instance. Provides a unified way to get the current time
/// throughout the application while maintaining testability.
/// </summary>
public static class Clock
{
    private static volatile IClock? _instance;

    /// <summary>
    /// Gets the current clock instance. 
    /// Returns the ambient scope clock if one is active, otherwise returns the initialized instance.
    /// Throws an exception if no clock has been initialized and no scope is active.
    /// </summary>
    public static IClock Instance => 
        ClockScope.Current ??
        _instance ?? 
        throw new InvalidOperationException(
            "Clock not initialized. Call Clock.Initialize(IClock) during application startup.");

    /// <summary>
    /// Initializes the clock instance in a thread-safe manner.
    /// This should be called exactly once during application startup.
    /// </summary>
    /// <param name="clock">The clock instance to use</param>
    /// <exception cref="ArgumentNullException">Thrown when clock is null</exception>
    public static void Initialize(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Interlocked.Exchange(ref _instance, clock);
    }

#if DEBUG
    /// <summary>
    /// Resets the clock instance to null. Used primarily for test cleanup.
    /// This method is only available in DEBUG builds.
    /// </summary>
    public static void Reset() => Interlocked.Exchange(ref _instance, null);
#endif
}