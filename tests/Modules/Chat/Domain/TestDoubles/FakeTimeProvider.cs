using Axon.Modules.Chat.Domain.Tests.Common;

namespace Axon.Modules.Chat.Domain.Tests.TestDoubles;

/// <summary>
/// Fake TimeProvider for deterministic testing.
/// Allows tests to control the passage of time and verify time-dependent behavior.
/// Implements both fixed time and advancing time scenarios.
/// </summary>
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _currentTime;
    private readonly TimeSpan _autoAdvanceInterval;
    private bool _autoAdvance;

    /// <summary>
    /// Creates a FakeTimeProvider that always returns the specified time.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset fixedTime)
    {
        _currentTime = fixedTime;
        _autoAdvanceInterval = TimeSpan.Zero;
        _autoAdvance = false;
    }

    /// <summary>
    /// Creates a FakeTimeProvider with default test time.
    /// </summary>
    public FakeTimeProvider() : this(TestConstants.DateTimes.DefaultTestTime) { }

    /// <summary>
    /// Creates a FakeTimeProvider that auto-advances by the specified interval on each call.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset startTime, TimeSpan autoAdvanceInterval)
    {
        _currentTime = startTime;
        _autoAdvanceInterval = autoAdvanceInterval;
        _autoAdvance = true;
    }

    /// <summary>
    /// Gets the current time. If auto-advance is enabled, advances time on each call.
    /// </summary>
    public override DateTimeOffset GetUtcNow()
    {
        var currentTime = _currentTime;
        if (_autoAdvance)
        {
            _currentTime = _currentTime.Add(_autoAdvanceInterval);
        }
        return currentTime;
    }

    /// <summary>
    /// Manually advances the current time by the specified amount.
    /// </summary>
    public void Advance(TimeSpan timeSpan)
    {
        _currentTime = _currentTime.Add(timeSpan);
    }

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    public void SetTime(DateTimeOffset newTime)
    {
        _currentTime = newTime;
    }

    /// <summary>
    /// Enables or disables auto-advance behavior.
    /// </summary>
    public void SetAutoAdvance(bool enabled)
    {
        _autoAdvance = enabled;
    }

    /// <summary>
    /// Gets the current time without advancing (useful for assertions).
    /// </summary>
    public DateTimeOffset CurrentTime => _currentTime;

    /// <summary>
    /// Creates a FakeTimeProvider for testing scenarios requiring time progression.
    /// </summary>
    public static FakeTimeProvider WithAutoAdvance(TimeSpan interval)
    {
        return new FakeTimeProvider(TestConstants.DateTimes.DefaultTestTime, interval);
    }

    /// <summary>
    /// Creates a FakeTimeProvider starting from a future time.
    /// </summary>
    public static FakeTimeProvider InTheFuture()
    {
        return new FakeTimeProvider(TestConstants.DateTimes.FutureTestTime);
    }
}