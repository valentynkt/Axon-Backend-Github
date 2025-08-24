using BuildingBlocks.Core.Domain.Events;

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
    protected static void AssertDomainEventProperties<TEvent>(
        TEvent domainEvent,
        Action<TEvent> propertyAssertions)
        where TEvent : class, IDomainEvent
    {
        domainEvent.ShouldNotBeNull("Domain event should not be null");
        domainEvent.EventId.ShouldNotBe(Guid.Empty, "Domain event should have a valid ID");
        domainEvent.OccurredAt.ShouldNotBe(default(DateTime), "Domain event should have a valid OccurredAt timestamp");
        
        // Perform custom property assertions
        propertyAssertions(domainEvent);
    }

    /// <summary>
    /// Asserts that a domain event occurred at the expected time.
    /// </summary>
    protected static void AssertEventOccurredAt<TEvent>(TEvent domainEvent, DateTime expectedTime)
        where TEvent : class, IDomainEvent
    {
        domainEvent.OccurredAt.ShouldBe(expectedTime, 
            $"Event should have occurred at {expectedTime} but was {domainEvent.OccurredAt}");
    }

    /// <summary>
    /// Asserts that a domain event occurred within the expected time range.
    /// </summary>
    protected static void AssertEventOccurredWithin<TEvent>(
        TEvent domainEvent, 
        DateTime startTime, 
        DateTime endTime)
        where TEvent : class, IDomainEvent
    {
        var occurredAt = domainEvent.OccurredAt;
        occurredAt.ShouldBeGreaterThanOrEqualTo(startTime, 
            $"Event should have occurred at or after {startTime}");
        occurredAt.ShouldBeLessThanOrEqualTo(endTime, 
            $"Event should have occurred at or before {endTime}");
    }

    /// <summary>
    /// Asserts that multiple domain events occurred in chronological order.
    /// </summary>
    protected static void AssertEventsInChronologicalOrder(params IDomainEvent[] events)
    {
        for (int i = 1; i < events.Length; i++)
        {
            events[i].OccurredAt.ShouldBeGreaterThanOrEqualTo(events[i - 1].OccurredAt,
                $"Event {i} should have occurred at or after event {i - 1}");
        }
    }

    /// <summary>
    /// Asserts that a domain event contains expected data and follows naming conventions.
    /// </summary>
    protected static void AssertWellFormedDomainEvent<TEvent>(
        TEvent domainEvent,
        string? expectedEventNameSuffix = "Event")
        where TEvent : class, IDomainEvent
    {
        // Basic structure
        domainEvent.ShouldNotBeNull("Domain event should not be null");
        domainEvent.EventId.ShouldNotBe(Guid.Empty, "Domain event should have a valid unique ID");
        
        // Timestamp validation
        domainEvent.OccurredAt.ShouldNotBe(default(DateTime), 
            "Domain event should have a valid OccurredAt timestamp");
        domainEvent.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddMinutes(1), 
            "Domain event timestamp should not be in the future");

        // Naming convention
        if (expectedEventNameSuffix != null)
        {
            var eventTypeName = typeof(TEvent).Name;
            eventTypeName.ShouldEndWith(expectedEventNameSuffix);
        }
    }

    /// <summary>
    /// Tests that a domain event can be reconstructed from its properties (useful for serialization scenarios).
    /// </summary>
    protected static void AssertEventCanBeReconstructed<TEvent>(
        TEvent originalEvent,
        Func<TEvent, TEvent> reconstructionFunction)
        where TEvent : class, IDomainEvent
    {
        var reconstructedEvent = reconstructionFunction(originalEvent);
        
        reconstructedEvent.ShouldNotBeNull("Reconstructed event should not be null");
        reconstructedEvent.EventId.ShouldBe(originalEvent.EventId, "Reconstructed event should have same ID");
        reconstructedEvent.OccurredAt.ShouldBe(originalEvent.OccurredAt, "Reconstructed event should have same timestamp");
    }

    /// <summary>
    /// Creates a collection of domain events for testing event ordering and batch operations.
    /// </summary>
    protected List<TEvent> CreateEventSequence<TEvent>(
        int count, 
        Func<int, DateTime, TEvent> eventFactory)
        where TEvent : class, IDomainEvent
    {
        var events = new List<TEvent>();
        var baseTime = CurrentTime.DateTime;

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
    protected static void AssertUniqueEventIds(IEnumerable<IDomainEvent> events)
    {
        var eventList = events.ToList();
        var uniqueIds = eventList.Select(e => e.EventId).Distinct().Count();
        
        uniqueIds.ShouldBe(eventList.Count, 
            "All domain events in the sequence should have unique IDs");
    }

    /// <summary>
    /// Asserts that an event contains all required audit information.
    /// </summary>
    protected static void AssertEventAuditInformation<TEvent>(TEvent domainEvent)
        where TEvent : class, IDomainEvent
    {
        domainEvent.EventId.ShouldNotBe(Guid.Empty, "Event should have a valid ID");
        domainEvent.OccurredAt.ShouldNotBe(default(DateTime), "Event should have a valid timestamp");
        
        // Additional audit checks can be added here as needed
        // e.g., user context, correlation IDs, etc.
    }
}