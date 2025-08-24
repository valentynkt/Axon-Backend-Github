using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Base class for testing aggregate roots in the Chat Domain.
/// Provides specialized utilities for testing aggregate behavior, invariants, and domain events.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type being tested</typeparam>
/// <typeparam name="TId">The strongly-typed ID of the aggregate</typeparam>
public abstract class AggregateTestBase<TAggregate, TId> : DomainTestBase
    where TAggregate : AggregateRoot<TId>
    where TId : class
{
    /// <summary>
    /// Asserts that the aggregate has raised the expected number of domain events.
    /// </summary>
    protected void AssertDomainEventCount(TAggregate aggregate, int expectedCount)
    {
        aggregate.DomainEvents.Count.ShouldBe(expectedCount, 
            $"Expected {expectedCount} domain events but found {aggregate.DomainEvents.Count}");
    }

    /// <summary>
    /// Asserts that the aggregate has raised a domain event of the specified type.
    /// Returns the first event of that type for further assertions.
    /// </summary>
    protected TEvent AssertDomainEventRaised<TEvent>(TAggregate aggregate) 
        where TEvent : class, IDomainEvent
    {
        var domainEvent = aggregate.DomainEvents.OfType<TEvent>().FirstOrDefault();
        domainEvent.ShouldNotBeNull($"Expected domain event of type {typeof(TEvent).Name} but none was found");
        return domainEvent;
    }

    /// <summary>
    /// Asserts that the aggregate has raised domain events of the specified types in order.
    /// </summary>
    protected void AssertDomainEventsRaisedInOrder(TAggregate aggregate, params Type[] eventTypes)
    {
        var domainEvents = aggregate.DomainEvents.ToList();
        domainEvents.Count.ShouldBe(eventTypes.Length, 
            $"Expected {eventTypes.Length} events but found {domainEvents.Count}");

        for (int i = 0; i < eventTypes.Length; i++)
        {
            domainEvents[i].GetType().ShouldBe(eventTypes[i], 
                $"Event at position {i} should be {eventTypes[i].Name} but was {domainEvents[i].GetType().Name}");
        }
    }

    /// <summary>
    /// Asserts that no domain events have been raised by the aggregate.
    /// </summary>
    protected void AssertNoDomainEventsRaised(TAggregate aggregate)
    {
        aggregate.DomainEvents.ShouldBeEmpty("Expected no domain events to be raised");
    }

    /// <summary>
    /// Asserts that the aggregate has not raised a domain event of the specified type.
    /// </summary>
    protected void AssertDomainEventNotRaised<TEvent>(TAggregate aggregate) 
        where TEvent : IDomainEvent
    {
        var hasEvent = aggregate.DomainEvents.OfType<TEvent>().Any();
        hasEvent.ShouldBeFalse($"Did not expect domain event of type {typeof(TEvent).Name} to be raised");
    }

    /// <summary>
    /// Clears all domain events from the aggregate.
    /// Useful for testing multiple operations on the same aggregate.
    /// </summary>
    protected void ClearDomainEvents(TAggregate aggregate)
    {
        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    /// <summary>
    /// Asserts that the aggregate's invariants are maintained.
    /// Override in specific aggregate test classes to implement custom invariant checks.
    /// </summary>
    protected virtual void AssertAggregateInvariants(TAggregate aggregate)
    {
        // Base implementation - can be overridden
        aggregate.ShouldNotBeNull();
        aggregate.Id.ShouldNotBeNull();
    }

    /// <summary>
    /// Performs a complete verification of aggregate state after an operation.
    /// Combines invariant checking with domain event verification.
    /// </summary>
    protected void AssertAggregateIsInValidState(
        TAggregate aggregate, 
        int? expectedEventCount = null,
        Action<TAggregate>? additionalAssertions = null)
    {
        // Check basic invariants
        AssertAggregateInvariants(aggregate);

        // Check event count if specified
        if (expectedEventCount.HasValue)
        {
            AssertDomainEventCount(aggregate, expectedEventCount.Value);
        }

        // Perform additional custom assertions
        additionalAssertions?.Invoke(aggregate);
    }
}