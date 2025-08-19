using Axon.Shared.Domain;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Extensions;

/// <summary>
/// Custom Shouldly assertions for domain-specific testing.
/// Provides fluent, expressive assertions for domain concepts.
/// </summary>
public static class DomainAssertions
{
    #region Aggregate Assertions

    /// <summary>
    /// Asserts that an aggregate has raised a specific domain event.
    /// </summary>
    public static void ShouldHaveRaisedEvent<TEvent>(this AggregateRoot aggregate)
        where TEvent : IDomainEvent
    {
        var events = aggregate.DomainEvents;
        var hasEvent = events.Any(e => e is TEvent);
        
        hasEvent.ShouldBeTrue(
            $"Expected aggregate to have raised {typeof(TEvent).Name} but found: {string.Join(", ", events.Select(e => e.GetType().Name))}");
    }

    /// <summary>
    /// Asserts that an aggregate has raised a specific event with validation.
    /// </summary>
    public static TEvent ShouldHaveRaisedEvent<TEvent>(
        this AggregateRoot aggregate,
        Action<TEvent> eventAssertion)
        where TEvent : IDomainEvent
    {
        aggregate.ShouldHaveRaisedEvent<TEvent>();
        
        var domainEvent = aggregate.DomainEvents.OfType<TEvent>().First();
        eventAssertion(domainEvent);
        return domainEvent;
    }

    /// <summary>
    /// Asserts that an aggregate has not raised a specific domain event.
    /// </summary>
    public static void ShouldNotHaveRaisedEvent<TEvent>(this AggregateRoot aggregate)
        where TEvent : IDomainEvent
    {
        var events = aggregate.DomainEvents;
        var hasEvent = events.Any(e => e is TEvent);
        
        hasEvent.ShouldBeFalse(
            $"Expected aggregate not to have raised {typeof(TEvent).Name}");
    }

    /// <summary>
    /// Asserts that an aggregate has raised exactly the specified number of events.
    /// </summary>
    public static void ShouldHaveRaisedEventCount(this AggregateRoot aggregate, int expectedCount)
    {
        var actualCount = aggregate.DomainEvents.Count;
        actualCount.ShouldBe(expectedCount,
            $"Expected {expectedCount} events but found {actualCount}");
    }

    /// <summary>
    /// Asserts that events contain a specific event type.
    /// </summary>
    public static void ShouldContainEvent<TEvent>(this IReadOnlyList<IDomainEvent> events)
        where TEvent : IDomainEvent
    {
        var hasEvent = events.Any(e => e is TEvent);
        hasEvent.ShouldBeTrue(
            $"Expected events to contain {typeof(TEvent).Name} but found: {string.Join(", ", events.Select(e => e.GetType().Name))}");
    }

    #endregion

    #region Conversation-Specific Assertions

    /// <summary>
    /// Asserts that a conversation has a specific message count.
    /// </summary>
    public static void ShouldHaveMessageCount(this Conversation conversation, int expectedCount)
    {
        conversation.MessageCount.ShouldBe(expectedCount,
            $"Expected conversation to have {expectedCount} messages but found {conversation.MessageCount}");
    }

    /// <summary>
    /// Asserts that a conversation is active.
    /// </summary>
    public static void ShouldBeActive(this Conversation conversation)
    {
        conversation.IsActive.ShouldBeTrue(
            $"Expected conversation to be active but status is {conversation.Status}");
    }

    /// <summary>
    /// Asserts that a conversation is completed.
    /// </summary>
    public static void ShouldBeCompleted(this Conversation conversation)
    {
        conversation.Status.ShouldBe(ConversationStatus.Completed,
            "Expected conversation to be completed");
    }

    /// <summary>
    /// Asserts that a conversation has a default title.
    /// </summary>
    public static void ShouldHaveDefaultTitle(this Conversation conversation)
    {
        conversation.HasDefaultTitle.ShouldBeTrue(
            "Expected conversation to have default title");
        conversation.Title.ShouldBeNull("Default title should be null");
    }

    /// <summary>
    /// Asserts that a conversation has a user-provided title.
    /// </summary>
    public static void ShouldHaveUserTitle(this Conversation conversation, string? expectedTitle = null)
    {
        conversation.HasDefaultTitle.ShouldBeFalse(
            "Expected conversation to have user-provided title");
        conversation.Title.ShouldNotBeNull("User-provided title should not be null");
        
        if (expectedTitle != null)
        {
            conversation.Title.ShouldBe(expectedTitle);
        }
    }

    /// <summary>
    /// Asserts that a conversation belongs to a specific user.
    /// </summary>
    public static void ShouldBelongTo(this Conversation conversation, UserId expectedOwner)
    {
        conversation.OwnerId.ShouldBe(expectedOwner,
            $"Expected conversation to belong to {expectedOwner} but belongs to {conversation.OwnerId}");
    }

    /// <summary>
    /// Asserts conversation timestamps are in correct order.
    /// </summary>
    public static void ShouldHaveValidTimestamps(this Conversation conversation)
    {
        conversation.CreatedAtUtc.ShouldBeLessThanOrEqualTo(conversation.UpdatedAtUtc,
            "Created time should be less than or equal to updated time");
        
        if (conversation.MessagesOrdered.Any())
        {
            var firstMessage = conversation.MessagesOrdered.First();
            firstMessage.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(conversation.CreatedAtUtc,
                "First message should be created after or at conversation creation");
        }
    }

    #endregion

    #region Event-Specific Assertions

    /// <summary>
    /// Asserts properties of ConversationStartedEvent.
    /// </summary>
    public static void ShouldBeValidStartedEvent(
        this ConversationStartedEvent startedEvent,
        ConversationId expectedId,
        UserId expectedOwner)
    {
        startedEvent.ConversationId.ShouldBe(expectedId);
        startedEvent.OwnerId.ShouldBe(expectedOwner);
        startedEvent.StartedAt.ShouldNotBe(default(DateTimeOffset));
    }

    /// <summary>
    /// Asserts properties of UserMessageAppendedEvent.
    /// </summary>
    public static void ShouldBeValidUserMessageEvent(
        this UserMessageAppendedEvent messageEvent,
        ConversationId expectedConversationId,
        int expectedSequence)
    {
        messageEvent.ConversationId.ShouldBe(expectedConversationId);
        messageEvent.Sequence.ShouldBe(expectedSequence);
        messageEvent.ContentPreview.ShouldNotBeNullOrEmpty();
        messageEvent.ContentPreview.Length.ShouldBeLessThanOrEqualTo(100);
    }

    /// <summary>
    /// Asserts properties of AssistantMessageAppendedEvent.
    /// </summary>
    public static void ShouldBeValidAssistantMessageEvent(
        this AssistantMessageAppendedEvent messageEvent,
        ConversationId expectedConversationId,
        int expectedSequence)
    {
        messageEvent.ConversationId.ShouldBe(expectedConversationId);
        messageEvent.Sequence.ShouldBe(expectedSequence);
        messageEvent.ContentPreview.ShouldNotBeNullOrEmpty();
        messageEvent.ContentPreview.Length.ShouldBeLessThanOrEqualTo(100);
    }

    /// <summary>
    /// Asserts properties of ConversationCompletedEvent.
    /// </summary>
    public static void ShouldBeValidCompletedEvent(
        this ConversationCompletedEvent completedEvent,
        ConversationId expectedId,
        int expectedMessageCount)
    {
        completedEvent.ConversationId.ShouldBe(expectedId);
        completedEvent.MessageCount.ShouldBe(expectedMessageCount);
        completedEvent.CompletedAt.ShouldNotBe(default(DateTimeOffset));
    }

    #endregion

    #region Collection Assertions

    /// <summary>
    /// Asserts that messages are in sequential order.
    /// </summary>
    public static void ShouldBeInSequentialOrder(this IReadOnlyList<Message> messages)
    {
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1,
                $"Message at index {i} should have sequence {i + 1} but has {messages[i].Sequence}");
        }
    }

    /// <summary>
    /// Asserts that messages are in chronological order.
    /// </summary>
    public static void ShouldBeInChronologicalOrder(this IReadOnlyList<Message> messages)
    {
        for (int i = 1; i < messages.Count; i++)
        {
            messages[i].CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(messages[i - 1].CreatedAtUtc,
                $"Message {i} should be created after message {i - 1}");
        }
    }

    /// <summary>
    /// Asserts that all items in a collection satisfy a condition.
    /// </summary>
    public static void ShouldAllSatisfy<T>(this IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        var items = collection.ToList();
        var failingItems = items.Where(item => !predicate(item)).ToList();
        
        failingItems.ShouldBeEmpty(
            message ?? $"{failingItems.Count} items failed the condition");
    }

    #endregion

    #region Time Assertions

    /// <summary>
    /// Asserts that a timestamp is recent (within the last minute).
    /// </summary>
    public static void ShouldBeRecent(this DateTimeOffset timestamp, TimeSpan? tolerance = null)
    {
        var maxAge = tolerance ?? TimeSpan.FromMinutes(1);
        var age = DateTimeOffset.UtcNow - timestamp;
        
        age.ShouldBeLessThan(maxAge,
            $"Timestamp {timestamp} is {age.TotalSeconds:F1} seconds old, expected less than {maxAge.TotalSeconds} seconds");
    }

    /// <summary>
    /// Asserts that a timestamp is approximately equal to expected time.
    /// </summary>
    public static void ShouldBeApproximately(
        this DateTimeOffset actual,
        DateTimeOffset expected,
        TimeSpan? tolerance = null)
    {
        var maxDifference = tolerance ?? TimeSpan.FromMilliseconds(100);
        var difference = Math.Abs((actual - expected).TotalMilliseconds);
        
        difference.ShouldBeLessThan(maxDifference.TotalMilliseconds,
            $"Expected {actual} to be within {maxDifference.TotalMilliseconds}ms of {expected}, but difference was {difference}ms");
    }

    #endregion
}