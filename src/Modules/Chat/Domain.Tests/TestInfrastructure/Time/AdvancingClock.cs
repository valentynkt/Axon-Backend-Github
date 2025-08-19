using BuildingBlocks.Core.Abstractions.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

/// <summary>
/// Test implementation of IClock that advances time by a fixed step on each UtcNow call.
/// Useful for sequence-dependent tests where you need predictable time progression.
/// </summary>
public sealed class AdvancingClock : IClock
{
    private DateTimeOffset _current;
    private readonly TimeSpan _step;

    /// <summary>
    /// Initializes a new instance of AdvancingClock with a seed time and step interval.
    /// </summary>
    /// <param name="seed">The initial time to start from.</param>
    /// <param name="step">The amount of time to advance on each UtcNow call.</param>
    public AdvancingClock(DateTimeOffset seed, TimeSpan step)
    {
        _current = seed;
        _step = step;
    }

    /// <summary>
    /// Returns the current time and then advances it by the step interval.
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = _current;
            _current = _current.Add(_step);
            return now;
        }
    }

    /// <summary>
    /// Factory method for creating an AdvancingClock that advances by 1 minute on each call.
    /// </summary>
    public static AdvancingClock StartingAt(DateTimeOffset seed) => 
        new(seed, TimeSpan.FromMinutes(1));

    /// <summary>
    /// Factory method for creating an AdvancingClock starting at the current system time.
    /// </summary>
    public static AdvancingClock StartingNow(TimeSpan step) => 
        new(DateTimeOffset.UtcNow, step);
}