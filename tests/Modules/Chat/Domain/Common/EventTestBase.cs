namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Base class for testing domain events in the Chat Domain.
/// Provides utilities for testing event creation, serialization, and behavior.
/// </summary>
public abstract class EventTestBase : DomainTestBase
{
    /// <summary>
    /// Asserts that a domain event has the expected properties.
    /// </summary>
    protected void AssertDomainEventProperties<TEvent>(
        TEvent domainEvent,
        Action<TEvent> propertyAssertions)
        where TEvent : IDomainEvent
    {
        domainEvent.ShouldNotBeNull("Domain event should not be null");
        domainEvent.Id.ShouldNotBe(Guid.Empty, "Domain event should have a valid ID");
        domainEvent.OccurredOn.ShouldNotBe(default(DateTimeOffset), "Domain event should have a valid OccurredOn timestamp");
        
        // Perform custom property assertions
        propertyAssertions(domainEvent);
    }

    /// <summary>
    /// Asserts that a domain event occurred at the expected time.
    /// </summary>
    protected void AssertEventOccurredAt<TEvent>(TEvent domainEvent, DateTimeOffset expectedTime)
        where TEvent : IDomainEvent
    {
        domainEvent.OccurredOn.ShouldBe(expectedTime, 
            $"Event should have occurred at {expectedTime} but was {domainEvent.OccurredOn}");
    }

    /// <summary>
    /// Asserts that a domain event occurred within the expected time range.
    /// </summary>
    protected void AssertEventOccurredWithin<TEvent>(
        TEvent domainEvent, 
        DateTimeOffset startTime, 
        DateTimeOffset endTime)
        where TEvent : IDomainEvent
    {
        domainEvent.OccurredOn.ShouldBeGreaterThanOrEqualTo(startTime, 
            $"Event should have occurred at or after {startTime}");
        domainEvent.OccurredOn.ShouldBeLessThanOrEqualTo(endTime, 
            $"Event should have occurred at or before {endTime}");
    }

    /// <summary>
    /// Asserts that multiple domain events occurred in chronological order.
    /// </summary>
    protected void AssertEventsInChronologicalOrder(params IDomainEvent[] events)
    {
        for (int i = 1; i < events.Length; i++)
        {
            events[i].OccurredOn.ShouldBeGreaterThanOrEqualTo(events[i - 1].OccurredOn,
                $"Event {i} should have occurred at or after event {i - 1}");
        }
    }

    /// <summary>
    /// Asserts that a domain event contains expected data and follows naming conventions.
    /// </summary>
    protected void AssertWellFormedDomainEvent<TEvent>(
        TEvent domainEvent,
        string? expectedEventNameSuffix = "Event")
        where TEvent : IDomainEvent
    {
        // Basic structure
        domainEvent.ShouldNotBeNull("Domain event should not be null");
        domainEvent.Id.ShouldNotBe(Guid.Empty, "Domain event should have a valid unique ID");
        
        // Timestamp validation
        domainEvent.OccurredOn.ShouldNotBe(default(DateTimeOffset), 
            "Domain event should have a valid OccurredOn timestamp");
        domainEvent.OccurredOn.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(1), 
            "Domain event timestamp should not be in the future");

        // Naming convention
        if (expectedEventNameSuffix != null)
        {
            var eventTypeName = typeof(TEvent).Name;
            eventTypeName.ShouldEndWith(expectedEventNameSuffix, 
                $"Domain event type name should end with '{expectedEventNameSuffix}'");
        }
    }

    /// <summary>
    /// Tests that a domain event can be reconstructed from its properties (useful for serialization scenarios).
    /// </summary>
    protected void AssertEventCanBeReconstructed<TEvent>(
        TEvent originalEvent,
        Func<TEvent, TEvent> reconstructionFunction)
        where TEvent : IDomainEvent
    {
        var reconstructedEvent = reconstructionFunction(originalEvent);
        
        reconstructedEvent.ShouldNotBeNull("Reconstructed event should not be null");
        reconstructedEvent.Id.ShouldBe(originalEvent.Id, "Reconstructed event should have same ID");
        reconstructedEvent.OccurredOn.ShouldBe(originalEvent.OccurredOn, "Reconstructed event should have same timestamp");
    }

    /// <summary>
    /// Creates a collection of domain events for testing event ordering and batch operations.
    /// </summary>
    protected List<TEvent> CreateEventSequence<TEvent>(
        int count, 
        Func<int, DateTimeOffset, TEvent> eventFactory)
        where TEvent : IDomainEvent
    {
        var events = new List<TEvent>();
        var baseTime = CurrentTime;

        for (int i = 0; i < count; i++)
        {
            var eventTime = baseTime.AddSeconds(i);
            var domainEvent = eventFactory(i, eventTime);
            events.Add(domainEvent);
        }

        return events;
    }

    /// <summary>
    /// Asserts that events in a sequence have unique IDs.
    /// </summary>
    protected void AssertUniqueEventIds(IEnumerable<IDomainEvent> events)
    {
        var eventList = events.ToList();
        var uniqueIds = eventList.Select(e => e.Id).Distinct().Count();
        
        uniqueIds.ShouldBe(eventList.Count, 
            "All domain events in the sequence should have unique IDs");
    }

    /// <summary>
    /// Asserts that an event contains all required audit information.
    /// </summary>
    protected void AssertEventAuditInformation<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent
    {
        domainEvent.Id.ShouldNotBe(Guid.Empty, "Event should have a valid ID");
        domainEvent.OccurredOn.ShouldNotBe(default(DateTimeOffset), "Event should have a valid timestamp");
        
        // Additional audit checks can be added here as needed
        // e.g., user context, correlation IDs, etc.
    }
}