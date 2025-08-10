using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Base;

/// <summary>
/// Base class for all domain tests providing common setup, teardown, and utilities.
/// Implements world-class testing patterns for DDD.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
public abstract class DomainTestBase
{
    protected IClock Clock { get; private set; } = null!;
    protected DateTimeOffset TestTime { get; private set; }
    protected Random Random { get; private set; } = null!;

    /// <summary>
    /// Setup method called before each test.
    /// Override in derived classes to add specific setup logic.
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        // Use a fixed time for deterministic tests
        TestTime = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        Clock = new FixedClock(TestTime);
        Random = new Random(42); // Fixed seed for reproducibility
        
        // Clear any domain events from previous tests
        ClearDomainEvents();
        
        // Additional setup
        OnSetUp();
    }

    /// <summary>
    /// Teardown method called after each test.
    /// Override in derived classes to add specific cleanup logic.
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        OnTearDown();
        
        // Ensure no domain events leak between tests
        ClearDomainEvents();
    }

    /// <summary>
    /// Override this method to add custom setup logic.
    /// </summary>
    protected virtual void OnSetUp() { }

    /// <summary>
    /// Override this method to add custom teardown logic.
    /// </summary>
    protected virtual void OnTearDown() { }

    /// <summary>
    /// Advances the test clock by the specified amount.
    /// </summary>
    protected void AdvanceTime(TimeSpan timeSpan)
    {
        TestTime = TestTime.Add(timeSpan);
        Clock = new FixedClock(TestTime);
    }

    /// <summary>
    /// Sets the test clock to a specific time.
    /// </summary>
    protected void SetTime(DateTimeOffset time)
    {
        TestTime = time;
        Clock = new FixedClock(TestTime);
    }

    /// <summary>
    /// Creates a clock with a specific time for isolated test scenarios.
    /// </summary>
    protected IClock CreateClock(DateTimeOffset time)
    {
        return new FixedClock(time);
    }

    /// <summary>
    /// Generates a random string of specified length.
    /// </summary>
    protected string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Generates a random email address.
    /// </summary>
    protected string RandomEmail()
    {
        return $"{RandomString(10).ToLower()}@test.com";
    }

    /// <summary>
    /// Generates a random integer within a range.
    /// </summary>
    protected int RandomInt(int min = 0, int max = 100)
    {
        return Random.Next(min, max + 1);
    }

    /// <summary>
    /// Executes an action and captures any thrown exception.
    /// </summary>
    protected Exception? Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Executes an async function and captures any thrown exception.
    /// </summary>
    protected async Task<Exception?> CatchAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Asserts that an action completes within a specified timeout.
    /// </summary>
    protected async Task ShouldCompleteWithin(Func<Task> action, TimeSpan timeout)
    {
        var task = action();
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        
        if (completedTask != task)
        {
            throw new TimeoutException($"Operation did not complete within {timeout.TotalMilliseconds}ms");
        }
        
        await task; // Propagate any exceptions
    }

    /// <summary>
    /// Clears all domain events from the current context.
    /// </summary>
    private void ClearDomainEvents()
    {
        // This would typically interact with your domain event dispatcher
        // For now, it's a placeholder for cleanup logic
    }

    /// <summary>
    /// Asserts that a condition is eventually met within a timeout period.
    /// Useful for testing eventual consistency scenarios.
    /// </summary>
    protected async Task EventuallyAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollingInterval = null)
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeoutValue)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(interval);
        }

        throw new TimeoutException($"Condition was not met within {timeoutValue.TotalSeconds} seconds");
    }

    /// <summary>
    /// Helper method for arranging test data using a builder pattern.
    /// </summary>
    protected T Arrange<T>(Func<T> builder)
    {
        return builder();
    }

    /// <summary>
    /// Helper method for acting on test subjects with clear intent.
    /// </summary>
    protected TResult Act<TResult>(Func<TResult> action)
    {
        return action();
    }

    /// <summary>
    /// Helper method for acting on test subjects without return value.
    /// </summary>
    protected void Act(Action action)
    {
        action();
    }
}