using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Centralized assertion helpers for Chat module tests.
/// Provides fluent assertions and common validation patterns.
/// </summary>
public static class ChatTestAssertions
{
    #region Conversation Assertions

    /// <summary>
    /// Fluent assertions for Conversation aggregate.
    /// </summary>
    public static ConversationAssertions ShouldHave(this Conversation conversation)
    {
        return new ConversationAssertions(conversation);
    }

    public class ConversationAssertions
    {
        private readonly Conversation _conversation;

        public ConversationAssertions(Conversation conversation)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        }

        public ConversationAssertions MessageCount(int expected)
        {
            _conversation.GetMessageCount().ShouldBe(expected,
                $"Expected {expected} messages but found {_conversation.GetMessageCount()}");
            return this;
        }

        public ConversationAssertions Status(ConversationStatus expected)
        {
            _conversation.Status.ShouldBe(expected,
                $"Expected status {expected} but was {_conversation.Status}");
            return this;
        }

        public ConversationAssertions Title(string expected)
        {
            _conversation.Title.ShouldBe(expected,
                $"Expected title '{expected}' but was '{_conversation.Title}'");
            return this;
        }

        public ConversationAssertions UserMessages(int expected)
        {
            var userMessages = _conversation.GetMessagesByRole(MessageRole.User);
            userMessages.Count.ShouldBe(expected,
                $"Expected {expected} user messages but found {userMessages.Count}");
            return this;
        }

        public ConversationAssertions AssistantMessages(int expected)
        {
            var assistantMessages = _conversation.GetMessagesByRole(MessageRole.Assistant);
            assistantMessages.Count.ShouldBe(expected,
                $"Expected {expected} assistant messages but found {assistantMessages.Count}");
            return this;
        }

        public ConversationAssertions AlternatingMessageRoles()
        {
            var messages = _conversation.GetAllMessages();
            for (int i = 0; i < messages.Count; i++)
            {
                var expectedRole = (i % 2 == 0) ? MessageRole.User : MessageRole.Assistant;
                messages[i].Role.ShouldBe(expectedRole,
                    $"Message at position {i} should be {expectedRole} but was {messages[i].Role}");
            }
            return this;
        }

        public ConversationAssertions ConsecutiveSequences()
        {
            var messages = _conversation.GetAllMessages();
            for (int i = 0; i < messages.Count; i++)
            {
                messages[i].Sequence.ShouldBe(i + 1,
                    $"Message at index {i} should have sequence {i + 1} but has {messages[i].Sequence}");
            }
            return this;
        }

        public ConversationAssertions LastAiResponseId(AiResponseId? expected)
        {
            _conversation.LastAiResponseId.ShouldBe(expected,
                $"Expected LastAiResponseId to be {expected} but was {_conversation.LastAiResponseId}");
            return this;
        }
    }

    #endregion

    #region Message Assertions

    /// <summary>
    /// Fluent assertions for Message entity.
    /// </summary>
    public static MessageAssertions ShouldHave(this Message message)
    {
        return new MessageAssertions(message);
    }

    public class MessageAssertions
    {
        private readonly Message _message;

        public MessageAssertions(Message message)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public MessageAssertions Role(MessageRole expected)
        {
            _message.Role.ShouldBe(expected,
                $"Expected role {expected} but was {_message.Role}");
            return this;
        }

        public MessageAssertions Sequence(int expected)
        {
            _message.Sequence.ShouldBe(expected,
                $"Expected sequence {expected} but was {_message.Sequence}");
            return this;
        }

        public MessageAssertions Content(string expected)
        {
            _message.Content.Value.ShouldBe(expected,
                $"Expected content '{expected}' but was '{_message.Content.Value}'");
            return this;
        }

        public MessageAssertions ContentContaining(string substring)
        {
            _message.Content.Value.ShouldContain(substring);
            return this;
        }

        public MessageAssertions AiResponseId(AiResponseId? expected)
        {
            _message.AiResponseId.ShouldBe(expected,
                $"Expected AiResponseId {expected} but was {_message.AiResponseId}");
            return this;
        }

        public MessageAssertions NotDeleted()
        {
            _message.IsDeleted.ShouldBeFalse("Message should not be deleted");
            return this;
        }
    }

    #endregion

    #region Collection Assertions

    /// <summary>
    /// Asserts message collection properties.
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
    /// Asserts alternating message roles pattern.
    /// </summary>
    public static void ShouldAlternateRoles(this IReadOnlyList<Message> messages, MessageRole? startingRole = null)
    {
        var actualStartingRole = startingRole ?? MessageRole.User;
        for (int i = 0; i < messages.Count; i++)
        {
            var expectedRole = (i % 2 == 0) ? actualStartingRole :
                (actualStartingRole == MessageRole.User ? MessageRole.Assistant : MessageRole.User);

            messages[i].Role.ShouldBe(expectedRole,
                $"Message at position {i} should be {expectedRole} but was {messages[i].Role}");
        }
    }

    #endregion

    #region Concurrency Assertions

    /// <summary>
    /// Asserts that an operation should throw a concurrency exception.
    /// </summary>
    public static async Task ShouldThrowConcurrencyException(Func<Task> operation, string? message = null)
    {
        var exception = await Should.ThrowAsync<Exception>(operation);

        var isConcurrencyException = exception is ConcurrencyException ||
                                     exception is DbUpdateConcurrencyException;

        isConcurrencyException.ShouldBeTrue(
            message ?? $"Expected ConcurrencyException but got {exception.GetType().Name}: {exception.Message}");
    }

    /// <summary>
    /// Asserts that exactly one of two concurrent operations succeeds.
    /// </summary>
    public static void ShouldHaveOneConcurrentSuccess(bool firstSucceeded, bool secondSucceeded)
    {
        (firstSucceeded ^ secondSucceeded).ShouldBeTrue(
            $"Exactly one operation should succeed in concurrent scenario. " +
            $"First: {firstSucceeded}, Second: {secondSucceeded}");
    }

    #endregion

    #region Database Constraint Assertions

    /// <summary>
    /// Asserts PostgreSQL constraint violation with specific constraint name.
    /// </summary>
    public static async Task ShouldViolateConstraint(
        Func<Task> operation,
        string expectedConstraintPattern,
        string? constraintType = null)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(operation);

        var postgresException = exception.InnerException as PostgresException;
        postgresException.ShouldNotBeNull("Expected PostgreSQL constraint violation");

        if (constraintType != null)
        {
            var expectedSqlState = constraintType switch
            {
                "unique" => "23505",
                "foreign_key" => "23503",
                "check" => "23514",
                "not_null" => "23502",
                _ => null
            };

            if (expectedSqlState != null)
            {
                postgresException.SqlState.ShouldBe(expectedSqlState,
                    $"Expected {constraintType} constraint violation");
            }
        }

        if (!string.IsNullOrEmpty(expectedConstraintPattern))
        {
            postgresException.ConstraintName.ShouldNotBeNull();
            postgresException.ConstraintName.ShouldContain(expectedConstraintPattern);
        }
    }

    #endregion

    #region State Validation Assertions

    /// <summary>
    /// Comprehensive conversation state validation.
    /// </summary>
    public static async Task ShouldBeInValidState(
        this Conversation conversation,
        Func<Task<Conversation?>> reloadFunc)
    {
        // Validate in-memory state
        conversation.GetAllMessages().ShouldBeInSequentialOrder();

        // Reload and validate persisted state
        var reloaded = await reloadFunc();
        reloaded.ShouldNotBeNull("Conversation should be persisted");

        // Compare states
        reloaded.Id.ShouldBe(conversation.Id);
        reloaded.Status.ShouldBe(conversation.Status);
        reloaded.GetMessageCount().ShouldBe(conversation.GetMessageCount());
    }

    #endregion

    #region Performance Assertions

    /// <summary>
    /// Asserts operation completes within time limit.
    /// </summary>
    public static async Task ShouldCompleteWithin(
        this Task operation,
        TimeSpan timeout,
        string operationName)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await operation;
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeLessThanOrEqualTo(timeout,
            $"{operationName} took {stopwatch.Elapsed.TotalMilliseconds:F2}ms, " +
            $"exceeding threshold of {timeout.TotalMilliseconds:F2}ms");
    }

    #endregion
}