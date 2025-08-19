using BuildingBlocks.Core.Abstractions.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

/// <summary>
/// Test implementation of IClock that always returns a fixed instant in time.
/// Useful for deterministic tests that need consistent timestamps.
/// </summary>
public sealed class FixedClock : IClock
{
    private readonly DateTimeOffset _fixedInstant;

    /// <summary>
    /// Initializes a new instance of FixedClock with the specified fixed instant.
    /// </summary>
    /// <param name="fixedInstant">The fixed instant to return on every UtcNow call.</param>
    public FixedClock(DateTimeOffset fixedInstant)
    {
        _fixedInstant = fixedInstant;
    }

    /// <summary>
    /// Always returns the fixed instant provided in the constructor.
    /// </summary>
    public DateTimeOffset UtcNow => _fixedInstant;

    /// <summary>
    /// Factory method for creating a FixedClock with the current system time.
    /// Useful for tests that need to freeze time at "now".
    /// </summary>
    public static FixedClock At(DateTimeOffset instant) => new(instant);

    /// <summary>
    /// Factory method for creating a FixedClock at the current system time.
    /// </summary>
    public static FixedClock Now() => new(DateTimeOffset.UtcNow);
}