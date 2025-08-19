namespace BuildingBlocks.Core.Abstractions.Time;

/// <summary>
/// Provides a mechanism to temporarily override the current clock instance within a specific scope.
/// Uses AsyncLocal to ensure the override is isolated to the current execution context.
/// </summary>
public sealed class ClockScope : IDisposable
{
    private static readonly AsyncLocal<IClock?> _asyncLocalClock = new();
    private readonly IClock? _previousClock;
    private bool _disposed;

    private ClockScope(IClock clock)
    {
        _previousClock = _asyncLocalClock.Value;
        _asyncLocalClock.Value = clock;
    }

    /// <summary>
    /// Gets the current ambient clock if one is set in the current scope.
    /// </summary>
    internal static IClock? Current => _asyncLocalClock.Value;

    /// <summary>
    /// Creates a new clock scope that overrides the current clock instance.
    /// The override will be in effect until the returned scope is disposed.
    /// </summary>
    /// <param name="clock">The clock to use within this scope</param>
    /// <returns>A disposable scope that restores the previous clock when disposed</returns>
    /// <exception cref="ArgumentNullException">Thrown when clock is null</exception>
    public static ClockScope Override(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new ClockScope(clock);
    }

    /// <summary>
    /// Disposes the scope and restores the previous clock instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _asyncLocalClock.Value = _previousClock;
            _disposed = true;
        }
    }
}