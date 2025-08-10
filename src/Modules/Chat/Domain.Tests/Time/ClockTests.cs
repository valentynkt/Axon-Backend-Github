using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using Axon.Modules.Chat.Domain.Time;
using Shouldly;
using Xunit;

namespace Axon.Modules.Chat.Domain.Tests.Time;

public class ClockTests
{
    [Fact]
    public void SystemClock_Should_Return_Current_UtcTime()
    {
        // Arrange
        var systemClock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        
        // Act
        var clockTime = systemClock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        // Assert
        clockTime.ShouldBeGreaterThanOrEqualTo(before);
        clockTime.ShouldBeLessThanOrEqualTo(after);
        clockTime.Offset.ShouldBe(TimeSpan.Zero, "SystemClock should return UTC time");
    }

    [Fact]
    public void FixedClock_Should_Always_Return_Same_Time()
    {
        // Arrange
        var fixedInstant = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.Zero);
        var fixedClock = new FixedClock(fixedInstant);

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            fixedClock.UtcNow.ShouldBe(fixedInstant);
        }
    }

    [Fact]
    public void FixedClock_At_Should_Create_Clock_With_Specified_Time()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2023, 12, 25, 12, 0, 0, TimeSpan.Zero);

        // Act
        var clock = FixedClock.At(specificTime);

        // Assert
        clock.UtcNow.ShouldBe(specificTime);
    }

    [Fact]
    public void AdvancingClock_Should_Advance_Time_On_Each_Call()
    {
        // Arrange
        var seed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var step = TimeSpan.FromMinutes(5);
        var advancingClock = new AdvancingClock(seed, step);

        // Act & Assert
        var first = advancingClock.UtcNow;
        var second = advancingClock.UtcNow;
        var third = advancingClock.UtcNow;

        first.ShouldBe(seed);
        second.ShouldBe(seed.Add(step));
        third.ShouldBe(seed.Add(step).Add(step));
    }

    [Fact]
    public void AdvancingClock_StartingAt_Should_Create_Clock_With_Default_Step()
    {
        // Arrange
        var seed = new DateTimeOffset(2024, 6, 1, 14, 30, 0, TimeSpan.Zero);

        // Act
        var clock = AdvancingClock.StartingAt(seed);
        var first = clock.UtcNow;
        var second = clock.UtcNow;

        // Assert
        first.ShouldBe(seed);
        second.ShouldBe(seed.AddMinutes(1), "Default step should be 1 minute");
    }
}