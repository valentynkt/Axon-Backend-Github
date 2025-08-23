namespace Axon.Modules.Chat.Domain.Tests.Common;

/// <summary>
/// Test helpers for domain events in the Chat Domain.
/// Provides utilities for testing domain event publication, handling, and verification.
/// </summary>
public static class DomainEventTestHelpers
{
    /// <summary>
    /// Captures domain events raised by an aggregate during an operation.
    /// Useful for testing that the correct events are published.
    /// </summary>
    public static List<IDomainEvent> CaptureEventsFrom<TAggregate, TId>(
        TAggregate aggregate, 
        Action<TAggregate> operation)
        where TAggregate : AggregateRoot<TId>
        where TId : struct
    {
        // Clear existing events
        aggregate.ClearDomainEvents();
        
        // Perform the operation
        operation(aggregate);
        
        // Capture and return the events
        var events = aggregate.DomainEvents.ToList();
        return events;
    }

    /// <summary>
    /// Asserts that specific domain events were raised in order during an operation.
    /// </summary>
    public static List<IDomainEvent> AssertEventsRaisedDuring<TAggregate, TId>(
        TAggregate aggregate,
        Action<TAggregate> operation,
        params Type[] expectedEventTypes)
        where TAggregate : AggregateRoot<TId>
        where TId : struct
    {
        var events = CaptureEventsFrom(aggregate, operation);
        
        events.Count.ShouldBe(expectedEventTypes.Length, 
            $"Expected {expectedEventTypes.Length} events but found {events.Count}");

        for (int i = 0; i < expectedEventTypes.Length; i++)
        {
            events[i].GetType().ShouldBe(expectedEventTypes[i], 
                $"Event at position {i} should be {expectedEventTypes[i].Name} but was {events[i].GetType().Name}");
        }

        return events;
    }

    /// <summary>
    /// Verifies that domain events contain expected data and are properly formed.
    /// </summary>
    public static void VerifyEventProperties<TEvent>(
        TEvent domainEvent,
        Action<TEvent> propertyVerification)
        where TEvent : IDomainEvent
    {
        // Verify basic event structure
        domainEvent.ShouldNotBeNull("Domain event should not be null");
        domainEvent.Id.ShouldNotBe(Guid.Empty, "Domain event should have a valid ID");
        domainEvent.OccurredOn.ShouldNotBe(default(DateTimeOffset), "Domain event should have a valid timestamp");
        
        // Verify custom properties
        propertyVerification(domainEvent);
    }

    /// <summary>
    /// Creates a test scenario for verifying event ordering and timing.
    /// </summary>
    public static void VerifyEventSequence(
        List<IDomainEvent> events,
        DateTimeOffset expectedStartTime,
        TimeSpan? maxDuration = null)
    {
        events.ShouldNotBeEmpty("Event sequence should not be empty");

        // Verify events are in chronological order
        for (int i = 1; i < events.Count; i++)
        {
            events[i].OccurredOn.ShouldBeGreaterThanOrEqualTo(events[i - 1].OccurredOn,
                $"Event {i} should occur at or after event {i - 1}");
        }

        // Verify timing constraints
        var firstEvent = events.First();
        var lastEvent = events.Last();

        firstEvent.OccurredOn.ShouldBeGreaterThanOrEqualTo(expectedStartTime,
            "First event should occur at or after expected start time");

        if (maxDuration.HasValue)
        {
            var actualDuration = lastEvent.OccurredOn - firstEvent.OccurredOn;
            actualDuration.ShouldBeLessThanOrEqualTo(maxDuration.Value,
                $"Event sequence duration should not exceed {maxDuration.Value}");
        }
    }

    /// <summary>
    /// Filters events by type and returns strongly typed collection.
    /// </summary>
    public static List<TEvent> FilterEventsByType<TEvent>(IEnumerable<IDomainEvent> events)
        where TEvent : IDomainEvent
    {
        return events.OfType<TEvent>().ToList();
    }

    /// <summary>
    /// Verifies that events have unique IDs within a sequence.
    /// </summary>
    public static void VerifyUniqueEventIds(IEnumerable<IDomainEvent> events)
    {
        var eventList = events.ToList();
        var uniqueIds = eventList.Select(e => e.Id).Distinct().Count();
        
        uniqueIds.ShouldBe(eventList.Count, 
            "All events in the sequence should have unique IDs");
    }

    /// <summary>
    /// Creates mock domain events for testing event handling scenarios.
    /// </summary>
    public static class MockEvents
    {
        public static ConversationStartedEvent CreateConversationStarted(
            ConversationId? conversationId = null,
            UserId? ownerId = null,
            string? title = null,
            DateTimeOffset? occurredOn = null)
        {
            return new ConversationStartedEvent(
                conversationId ?? ConversationId.New(),
                ownerId ?? TestConstants.Users.DefaultOwnerId,
                title ?? TestConstants.Conversations.DefaultTitle,
                occurredOn ?? TestConstants.DateTimes.DefaultTestTime);
        }

        public static UserMessageAppendedEvent CreateUserMessageAppended(
            ConversationId? conversationId = null,
            MessageId? messageId = null,
            int sequence = 1,
            string? contentPreview = null,
            DateTimeOffset? occurredOn = null)
        {
            return new UserMessageAppendedEvent(
                conversationId ?? ConversationId.New(),
                messageId ?? MessageId.New(),
                sequence,
                contentPreview ?? TestConstants.Messages.DefaultUserMessage,
                occurredOn ?? TestConstants.DateTimes.DefaultTestTime);
        }

        public static AssistantMessageAppendedEvent CreateAssistantMessageAppended(
            ConversationId? conversationId = null,
            MessageId? messageId = null,
            int sequence = 2,
            string? contentPreview = null,
            AiResponseId? aiResponseId = null,
            DateTimeOffset? occurredOn = null)
        {
            return new AssistantMessageAppendedEvent(
                conversationId ?? ConversationId.New(),
                messageId ?? MessageId.New(),
                sequence,
                contentPreview ?? TestConstants.Messages.DefaultAssistantMessage,
                aiResponseId ?? TestConstants.AiResponses.DefaultAiResponseId,
                occurredOn ?? TestConstants.DateTimes.DefaultTestTime);
        }

        public static ConversationTitleUpdatedEvent CreateTitleUpdated(
            ConversationId? conversationId = null,
            string? newTitle = null,
            DateTimeOffset? occurredOn = null)
        {
            return new ConversationTitleUpdatedEvent(
                conversationId ?? ConversationId.New(),
                newTitle ?? TestConstants.Conversations.AlternativeTitle,
                occurredOn ?? TestConstants.DateTimes.DefaultTestTime);
        }

        public static ConversationCompletedEvent CreateConversationCompleted(
            ConversationId? conversationId = null,
            int messageCount = 2,
            DateTimeOffset? occurredOn = null)
        {
            return new ConversationCompletedEvent(
                conversationId ?? ConversationId.New(),
                messageCount,
                occurredOn ?? TestConstants.DateTimes.DefaultTestTime);
        }
    }

    /// <summary>
    /// Test patterns for common domain event scenarios.
    /// </summary>
    public static class Patterns
    {
        /// <summary>
        /// Tests the complete conversation lifecycle events.
        /// </summary>
        public static void VerifyConversationLifecycleEvents(
            List<IDomainEvent> events,
            bool shouldIncludeCompletion = false)
        {
            events.ShouldNotBeEmpty("Conversation lifecycle should produce events");

            // Should start with ConversationStartedEvent
            var firstEvent = events.First();
            firstEvent.ShouldBeOfType<ConversationStartedEvent>("First event should be ConversationStartedEvent");

            if (shouldIncludeCompletion)
            {
                // Should end with ConversationCompletedEvent
                var lastEvent = events.Last();
                lastEvent.ShouldBeOfType<ConversationCompletedEvent>("Last event should be ConversationCompletedEvent");
            }
        }

        /// <summary>
        /// Tests the alternating user/assistant message pattern.
        /// </summary>
        public static void VerifyAlternatingMessageEvents(List<IDomainEvent> events)
        {
            var messageEvents = events.Where(e => 
                e is UserMessageAppendedEvent || e is AssistantMessageAppendedEvent).ToList();

            for (int i = 0; i < messageEvents.Count; i++)
            {
                if (i % 2 == 0)
                {
                    messageEvents[i].ShouldBeOfType<UserMessageAppendedEvent>(
                        $"Event at position {i} should be user message (even positions)");
                }
                else
                {
                    messageEvents[i].ShouldBeOfType<AssistantMessageAppendedEvent>(
                        $"Event at position {i} should be assistant message (odd positions)");
                }
            }
        }
    }
}