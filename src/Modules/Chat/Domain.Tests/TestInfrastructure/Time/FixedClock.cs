using Microsoft.Extensions.Time.Testing;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

/// <summary>
/// Test TimeProvider that always returns a fixed instant in time.
/// Useful for deterministic tests that need consistent timestamps.
/// This is a wrapper around Microsoft's FakeTimeProvider for backwards compatibility.
/// </summary>
public sealed class FixedClock : TimeProvider
{
    private readonly FakeTimeProvider _fakeTimeProvider;

    /// <summary>
    /// Initializes a new instance of FixedClock with the specified fixed instant.
    /// </summary>
    /// <param name="fixedInstant">The fixed instant to return on every GetUtcNow() call.</param>
    public FixedClock(DateTimeOffset fixedInstant)
    {
        _fakeTimeProvider = new FakeTimeProvider(fixedInstant);
    }

    /// <summary>
    /// Always returns the fixed instant provided in the constructor.
    /// </summary>
    public override DateTimeOffset GetUtcNow() => _fakeTimeProvider.GetUtcNow();

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