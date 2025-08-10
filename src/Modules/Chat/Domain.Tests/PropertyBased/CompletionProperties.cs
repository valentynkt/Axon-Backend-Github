using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Builders;
using Axon.Modules.Chat.Domain.ValueObjects;
using Shouldly;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Axon.Modules.Chat.Domain.Tests.PropertyBased;

/// <summary>
/// Property-based tests for conversation completion rules and state transitions.
/// Tests completion conditions and immutability after completion.
/// </summary>
public class CompletionProperties
{
    [Property(Arbitrary = new[] { typeof(CompletionGenerators) })]
    public Property NonEmptyConversations_ShouldCompleteSuccessfully(NonEmptyConversation conversation)
    {
        return Prop.ForAll(Gen.Constant(conversation), conv =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(conv.Roles);
            script.ExecuteOn(builder);
            
            var messageCountBeforeCompletion = builder.MessageCount;
            
            // Act
            var result = builder.TryComplete();
            
            // Assert
            return result.IsSuccess.Label($"Non-empty conversation with {conv.Roles.Count} messages should complete")
                .And(builder.IsCompleted).Label("Conversation should be marked as completed")
                .And(builder.MessageCount == messageCountBeforeCompletion)
                    .Label("Message count should not change during completion");
        });
    }

    [Property]
    public Property EmptyConversations_ShouldFailToComplete()
    {
        return Prop.ForAll(Gen.Constant(Unit.Default), _ =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            
            // Act
            var result = builder.TryComplete();
            
            // Assert
            return result.IsFailure.Label("Empty conversation should fail to complete")
                .And(result.Error.Code == "CHAT_CONVERSATION_EMPTY_ON_COMPLETE")
                    .Label("Should have correct error code from catalog")
                .And(!builder.IsCompleted).Label("Conversation should not be marked as completed");
        });
    }

    [Property(Arbitrary = new[] { typeof(CompletionGenerators) })]
    public Property CompletedConversations_ShouldRejectNewMessages(CompletedConversation completed)
    {
        return Prop.ForAll(Gen.Constant(completed), conv =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(conv.InitialRoles);
            script.ExecuteOn(builder);
            builder.Complete();
            
            var messageCountAfterCompletion = builder.MessageCount;
            
            // Act
            var userResult = builder.TryAppendUser("Should fail");
            var assistantResult = builder.TryAppendAssistant("Should also fail");
            
            // Assert
            return userResult.IsFailure.Label("Adding user message to completed conversation should fail")
                .And(assistantResult.IsFailure).Label("Adding assistant message to completed conversation should fail")
                .And(builder.MessageCount == messageCountAfterCompletion)
                    .Label("Message count should not change after failed attempts")
                .And(builder.IsCompleted).Label("Conversation should remain completed");
        });
    }

    [Property]
    public Property CompletionStateTransition_ShouldBeIrreversible()
    {
        return Prop.ForAll(Gen.Constant(["User message", "Assistant response"]), messages =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            foreach (var (message, index) in messages.Select((m, i) => (m, i)))
            {
                if (index % 2 == 0)
                    builder.AppendUser(message);
                else
                    builder.AppendAssistant(message);
            }
            
            // Act
            var initialCompleteResult = builder.TryComplete();
            var secondCompleteResult = builder.TryComplete();
            
            // Assert
            return initialCompleteResult.IsSuccess.Label("First completion should succeed")
                .And(secondCompleteResult.IsSuccess).Label("Second completion should be idempotent")
                .And(builder.IsCompleted).Label("Should remain completed");
        });
    }

    [Property(Arbitrary = new[] { typeof(CompletionGenerators) })]
    public Property DifferentConversationSizes_ShouldAllCompleteWhenNonEmpty(VariedSizeConversation varied)
    {
        return Prop.ForAll(Gen.Constant(varied), conv =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(conv.Roles);
            script.ExecuteOn(builder);
            
            // Act
            var result = builder.TryComplete();
            
            // Assert
            return result.IsSuccess.Label($"Conversation with {conv.Roles.Count} messages should complete")
                .And(builder.IsCompleted).Label("Should be marked as completed")
                .And(builder.MessageCount == conv.Roles.Count).Label("Should maintain all messages");
        });
    }

    [Property]
    public Property CompletionWithTitleUpdate_ShouldSucceed()
    {
        return Prop.ForAll(Gen.Constant(("Original title", "Updated title")), titles =>
        {
            // Arrange
            var (originalTitle, updatedTitle) = titles;
            var builder = ConversationBuilder.StartedWithTitle(originalTitle)
                .AppendUser("User message")
                .AppendAssistant("Assistant message");
            
            // Act
            var titleUpdateResult = builder.TryUpdateTitle(updatedTitle);
            var completionResult = builder.TryComplete();
            
            // Assert
            return titleUpdateResult.IsSuccess.Label("Title update should succeed before completion")
                .And(completionResult.IsSuccess).Label("Completion should succeed after title update")
                .And(builder.Conversation.Status == ConversationStatus.Completed).Label("Should be completed")
                .And(builder.Conversation.Title == updatedTitle).Label("Should maintain updated title");
        });
    }

    [Property]
    public Property CompletedConversation_ShouldRejectTitleUpdates()
    {
        return Prop.ForAll(Gen.Constant("New title after completion"), newTitle =>
        {
            // Arrange
            var builder = ConversationBuilder.Started()
                .AppendUser("Message")
                .Complete();
            
            var titleBeforeAttempt = builder.Conversation.Title;
            
            // Act
            var result = builder.TryUpdateTitle(newTitle);
            
            // Assert
            return result.IsFailure.Label("Title update should fail on completed conversation")
                .And(builder.Conversation.Title == titleBeforeAttempt)
                    .Label("Title should remain unchanged")
                .And(builder.IsCompleted).Label("Should remain completed");
        });
    }

    [Property(Arbitrary = new[] { typeof(CompletionGenerators) })]
    public Property CompletionTiming_ShouldUseFixedClock(TimedCompletion timedCompletion)
    {
        return Prop.ForAll(Gen.Constant(timedCompletion), timing =>
        {
            // Arrange
            var builder = ConversationBuilder.Started();
            var script = ConversationScript.Create(timing.Roles);
            script.ExecuteOn(builder);
            
            // Act
            var beforeCompletion = builder.Clock.UtcNow;
            builder.Complete();
            var afterCompletion = builder.Clock.UtcNow;
            
            // Assert
            return (beforeCompletion == afterCompletion).Label("Clock should be fixed during tests")
                .And(builder.IsCompleted).Label("Should be completed")
                .And(builder.AllMessages.All(m => m.CreatedAt <= afterCompletion))
                    .Label("All message timestamps should be consistent with clock");
        });
    }
}

/// <summary>
/// Custom generators for completion property-based testing.
/// </summary>
public static class CompletionGenerators
{
    public static Arbitrary<NonEmptyConversation> NonEmptyConversations() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new NonEmptyConversation([MessageRole.User])),
            Gen.Constant(new NonEmptyConversation([MessageRole.User, MessageRole.Assistant])),
            Gen.Constant(new NonEmptyConversation(RoleFactory.ValidTurnTaking(3))),
            Gen.Constant(new NonEmptyConversation(RoleFactory.AlternatingPattern(4))),
            Gen.Constant(new NonEmptyConversation(RoleFactory.AllUser(5)))
        ));

    public static Arbitrary<CompletedConversation> CompletedConversations() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new CompletedConversation([MessageRole.User])),
            Gen.Constant(new CompletedConversation([MessageRole.User, MessageRole.Assistant])),
            Gen.Constant(new CompletedConversation(RoleFactory.ValidTurnTaking(3)))
        ));

    public static Arbitrary<VariedSizeConversation> VariedSizeConversations() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new VariedSizeConversation(RoleFactory.AllUser(1))),
            Gen.Constant(new VariedSizeConversation(RoleFactory.AlternatingPattern(2))),
            Gen.Constant(new VariedSizeConversation(RoleFactory.ValidTurnTaking(10))),
            Gen.Constant(new VariedSizeConversation(RoleFactory.AllUser(25))),
            Gen.Constant(new VariedSizeConversation(RoleFactory.AlternatingPattern(50)))
        ));

    public static Arbitrary<TimedCompletion> TimedCompletions() =>
        Arb.From(Gen.OneOf(
            Gen.Constant(new TimedCompletion([MessageRole.User])),
            Gen.Constant(new TimedCompletion([MessageRole.User, MessageRole.Assistant])),
            Gen.Constant(new TimedCompletion(RoleFactory.ValidTurnTaking(5)))
        ));
}

/// <summary>
/// Wrapper for non-empty conversations in property tests.
/// </summary>
public record NonEmptyConversation(List<MessageRole> Roles);

/// <summary>
/// Wrapper for completed conversations in property tests.
/// </summary>
public record CompletedConversation(List<MessageRole> InitialRoles);

/// <summary>
/// Wrapper for varied-size conversations in property tests.
/// </summary>
public record VariedSizeConversation(List<MessageRole> Roles);

/// <summary>
/// Wrapper for timed completion scenarios in property tests.
/// </summary>
public record TimedCompletion(List<MessageRole> Roles);