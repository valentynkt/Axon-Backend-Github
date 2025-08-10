using Axon.Shared.Domain;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Extensions;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Base;

/// <summary>
/// Base class for testing domain aggregates with comprehensive assertion support.
/// Provides utilities for testing aggregate behavior, state transitions, and event emission.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Aggregate")]
public abstract class AggregateTestBase<TAggregate> : DomainTestBase
    where TAggregate : AggregateRoot
{
    /// <summary>
    /// Creates an aggregate and captures its initial events.
    /// </summary>
    protected (TAggregate Aggregate, List<IDomainEvent> Events) CreateAggregateWithEvents(
        Func<TAggregate> factory)
    {
        var aggregate = factory();
        var events = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();
        return (aggregate, events);
    }

    /// <summary>
    /// Executes a command on an aggregate and returns the emitted events.
    /// </summary>
    protected List<IDomainEvent> ExecuteCommand(
        TAggregate aggregate,
        Action<TAggregate> command)
    {
        aggregate.ClearDomainEvents();
        command(aggregate);
        return aggregate.DomainEvents.ToList();
    }

    /// <summary>
    /// Executes a command and asserts specific events were raised.
    /// </summary>
    protected void ExecuteCommandAndAssertEvents<TEvent>(
        TAggregate aggregate,
        Action<TAggregate> command,
        Action<TEvent>? eventAssertion = null)
        where TEvent : IDomainEvent
    {
        var events = ExecuteCommand(aggregate, command);
        
        events.ShouldContainEvent<TEvent>();
        
        if (eventAssertion != null)
        {
            var domainEvent = events.OfType<TEvent>().First();
            eventAssertion(domainEvent);
        }
    }

    /// <summary>
    /// Asserts that a command raises exactly the specified events in order.
    /// </summary>
    protected void AssertEventsInOrder(
        TAggregate aggregate,
        Action<TAggregate> command,
        params Type[] expectedEventTypes)
    {
        var events = ExecuteCommand(aggregate, command);
        
        events.Count.ShouldBe(expectedEventTypes.Length,
            $"Expected {expectedEventTypes.Length} events but got {events.Count}");
        
        for (int i = 0; i < expectedEventTypes.Length; i++)
        {
            events[i].GetType().ShouldBe(expectedEventTypes[i],
                $"Event at position {i} should be {expectedEventTypes[i].Name} but was {events[i].GetType().Name}");
        }
    }

    /// <summary>
    /// Asserts that no events are raised by a command.
    /// </summary>
    protected void AssertNoEvents(TAggregate aggregate, Action<TAggregate> command)
    {
        var events = ExecuteCommand(aggregate, command);
        events.ShouldBeEmpty("Expected no events to be raised");
    }

    /// <summary>
    /// Tests that an aggregate maintains its invariants after a series of operations.
    /// </summary>
    protected void AssertInvariantsHold(
        TAggregate aggregate,
        params Action<TAggregate>[] operations)
    {
        foreach (var operation in operations)
        {
            operation(aggregate);
            AssertInvariants(aggregate);
        }
    }

    /// <summary>
    /// Override to define aggregate-specific invariant assertions.
    /// </summary>
    protected virtual void AssertInvariants(TAggregate aggregate)
    {
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldNotBeNull();
        // Override in derived classes to add specific invariant checks
    }

    /// <summary>
    /// Creates a Given-When-Then test scenario.
    /// </summary>
    protected void Scenario(
        string description,
        Func<TAggregate> given,
        Action<TAggregate> when,
        Action<TAggregate> then)
    {
        TestContext.WriteLine($"Scenario: {description}");
        
        // Given
        var aggregate = given();
        TestContext.WriteLine($"  Given: Aggregate in state {GetAggregateState(aggregate)}");
        
        // When
        when(aggregate);
        TestContext.WriteLine($"  When: Command executed");
        
        // Then
        then(aggregate);
        TestContext.WriteLine($"  Then: Assertions passed");
    }

    /// <summary>
    /// Creates an async Given-When-Then test scenario.
    /// </summary>
    protected async Task ScenarioAsync(
        string description,
        Func<Task<TAggregate>> given,
        Func<TAggregate, Task> when,
        Action<TAggregate> then)
    {
        TestContext.WriteLine($"Scenario: {description}");
        
        // Given
        var aggregate = await given();
        TestContext.WriteLine($"  Given: Aggregate in state {GetAggregateState(aggregate)}");
        
        // When
        await when(aggregate);
        TestContext.WriteLine($"  When: Async command executed");
        
        // Then
        then(aggregate);
        TestContext.WriteLine($"  Then: Assertions passed");
    }

    /// <summary>
    /// Tests aggregate behavior with multiple scenarios.
    /// </summary>
    protected void TestScenarios(params (string Description, Action Test)[] scenarios)
    {
        foreach (var (description, test) in scenarios)
        {
            TestContext.WriteLine($"Testing: {description}");
            test();
        }
    }

    /// <summary>
    /// Asserts that an operation fails with a specific error.
    /// </summary>
    protected void AssertFailure<TError>(
        Action operation,
        Action<TError>? errorAssertion = null)
        where TError : Exception
    {
        var exception = Catch(operation);
        
        exception.ShouldNotBeNull("Expected operation to throw an exception");
        exception.ShouldBeOfType<TError>();
        
        errorAssertion?.Invoke((TError)exception);
    }

    /// <summary>
    /// Gets a string representation of the aggregate's current state.
    /// Override for custom state descriptions.
    /// </summary>
    protected virtual string GetAggregateState(TAggregate aggregate)
    {
        return aggregate.GetType().Name;
    }

    /// <summary>
    /// Verifies that an aggregate can be reconstituted from events.
    /// </summary>
    protected void AssertEventSourcing(
        TAggregate originalAggregate,
        Func<IEnumerable<IDomainEvent>, TAggregate> reconstituteFunc)
    {
        var events = originalAggregate.DomainEvents.ToList();
        var reconstituted = reconstituteFunc(events);
        
        // Assert that key properties match
        reconstituted.Id.ShouldBe(originalAggregate.Id);
        AssertAggregatesEqual(originalAggregate, reconstituted);
    }

    /// <summary>
    /// Override to define how aggregates should be compared for equality.
    /// </summary>
    protected virtual void AssertAggregatesEqual(TAggregate expected, TAggregate actual)
    {
        // Override in derived classes for specific equality checks
        actual.Id.ShouldBe(expected.Id);
    }

    /// <summary>
    /// Tests that concurrent modifications are handled correctly.
    /// </summary>
    protected async Task AssertConcurrentSafety(
        Func<TAggregate> createAggregate,
        Action<TAggregate> operation,
        int concurrentOperations = 10)
    {
        var aggregate = createAggregate();
        var tasks = new List<Task>();
        
        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks.Add(Task.Run(() => operation(aggregate)));
        }
        
        await Task.WhenAll(tasks);
        
        // Verify aggregate is still in a valid state
        AssertInvariants(aggregate);
    }
}