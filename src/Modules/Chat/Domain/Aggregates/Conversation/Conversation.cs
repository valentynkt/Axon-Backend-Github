// /Axon/Modules/Chat/Domain/Aggregates/Conversation/Conversation.cs
#nullable enable
using System.Linq;
using System.Reflection;
using Axon.BuildingBlocks.Core.Constants;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Internal.Text;
using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Time.Testing;

namespace Axon.Modules.Chat.Domain.Aggregates.Conversation;

/// <summary>
/// Conversation aggregate root representing a chat conversation.
/// Enforces business rules and maintains conversation integrity.
/// </summary>
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();

// Core state
    public AxonUserId OwnerId { get; private set; }
    public ConversationStatus Status { get; private set; }
    public string? Title { get; private set; }

    /// <summary>
    /// The anchor for the next model call (use as 'previous_response_id').
    /// Set only when an assistant message is appended successfully.
    /// </summary>
    public AiResponseId? LastAiResponseId { get; private set; }

    // Helpers
    public bool IsActive => Status == ConversationStatus.Active;
    public bool HasDefaultTitle => Title is null;

    // Query Methods for Message Access
    /// <summary>Gets the total count of messages in the conversation.</summary>
    public int GetMessageCount() => _messages.Count;

    /// <summary>Checks if the conversation has any messages.</summary>
    public bool HasMessages() => _messages.Any();

    /// <summary>Gets a message by its sequence number.</summary>
    public Message? GetMessageBySequence(int sequence) => 
        _messages.FirstOrDefault(m => m.Sequence == sequence);

    /// <summary>Gets the latest message in the conversation.</summary>
    public Message? GetLatestMessage() => 
        _messages.MaxBy(m => m.Sequence);

    /// <summary>Gets all messages ordered by sequence.</summary>
    public IReadOnlyList<Message> GetAllMessages() => 
        _messages.OrderBy(m => m.Sequence).ToList().AsReadOnly();

    /// <summary>Gets messages within a specific sequence range.</summary>
    public IReadOnlyList<Message> GetMessagesInRange(int startSequence, int endSequence) =>
        _messages.Where(m => m.Sequence >= startSequence && m.Sequence <= endSequence)
                 .OrderBy(m => m.Sequence)
                 .ToList()
                 .AsReadOnly();

    /// <summary>Gets the last N messages from the conversation.</summary>
    public IReadOnlyList<Message> GetRecentMessages(int count) =>
        _messages.OrderByDescending(m => m.Sequence)
                 .Take(count)
                 .OrderBy(m => m.Sequence)
                 .ToList()
                 .AsReadOnly();

    /// <summary>Gets messages by role.</summary>
    public IReadOnlyList<Message> GetMessagesByRole(MessageRole role) =>
        _messages.Where(m => m.Role == role)
                 .OrderBy(m => m.Sequence)
                 .ToList()
                 .AsReadOnly();

    // EF Core parameterless ctor
    private Conversation() : base() { }

    private Conversation(ConversationId id, AxonUserId ownerId, string? title)
        : base(id)
    {
        OwnerId = ownerId;
        Status = ConversationStatus.Active;
        Title  = title;
    }

    /// <summary>
    /// Starts a new conversation with the specified owner.
    /// </summary>
    public static Result<Conversation, Error> StartNewConversation(
        AxonUserId ownerId,
        string? titleOrNull,
        TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustHaveOwnerRule(ownerId));
            CheckRule(new TitleProvidedMustBeValidRule(titleOrNull));

            // Use StronglyTypedIds factory over constructors
            var conversationId = ConversationId.New();
            var now = timeProvider.GetUtcNow();

            string? processedTitle = null;
            if (!string.IsNullOrWhiteSpace(titleOrNull))
            {
                var titleResult = ConversationTitle.Create(titleOrNull);
                if (titleResult.IsFailure)
                    return Result.Failure<Conversation, Error>(titleResult.Error);

                processedTitle = titleResult.Value.Value;
            }

            var conversation = new Conversation(conversationId, ownerId, processedTitle);

            // Override the CreatedAt with the provided TimeProvider instead of System time
            conversation.SetCreatedAt(now);

            conversation.RaiseDomainEvent(new ConversationStartedEvent(
                conversationId, ownerId, processedTitle, now));

            return Result.Success<Conversation, Error>(conversation);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Conversation, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Validates that a ConversationId is valid (not empty).
    /// </summary>
    public static Result<Unit, Error> ValidateConversationId(ConversationId conversationId)
    {
        try
        {
            CheckRule(new ConversationIdMustBeValidRule(conversationId));
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Validation(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Appends a user message to the conversation.
    /// </summary>
    public Result<Message, Error> AppendUserMessageToConversation(
        MessageContent content,
        TimeProvider timeProvider)
    {
        try
        {
            ValidateMessageAppendPreconditions(content.Value, MessageRole.User);
            CheckRule(new ConversationCanAcceptMoreMessagesRule(GetMessageCount()));
            CheckRule(new MessageContentMeetsDomainStandardsRule(content.Value));

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddUserMessage(content, timeProvider);

            RaiseUserMessageEvent(message, content.Value, now);

#if DEBUG
            CheckRule(new MessageSequenceIntegrityRule(_messages));
#endif
            return Result.Success<Message, Error>(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Message, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Appends an assistant message with AI response tracking and idempotency on AiResponseId.
    /// Assistant must follow user (turn-taking rule).
    /// </summary>
    public Result<Message, Error> AppendAssistantResponseToConversation(
        MessageContent content,
        AiResponseId aiResponseId,
        TimeProvider timeProvider)
    {
        try
        {
            ValidateMessageAppendPreconditions(content.Value, MessageRole.Assistant);
            CheckRule(new ConversationCanAcceptMoreMessagesRule(GetMessageCount()));
            CheckRule(new AssistantResponseContentValidRule(content.Value));
            CheckRule(new AiResponseIdMustBeUniqueRule(aiResponseId, _messages));

            // Idempotency: if message with same AI response id exists, return it
            var existing = FindExistingMessageByAiResponseId(aiResponseId);
            if (existing is not null)
                return Result.Success<Message, Error>(existing);

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddAssistantMessage(content, aiResponseId, timeProvider);

            RaiseAssistantMessageEvent(message, content.Value, aiResponseId, now);
            
            CheckRule(new MessageSequenceIntegrityRule(_messages));
            
            return Result.Success<Message, Error>(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Message, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Appends a user message and assistant response in a single operation.
    /// Useful for reducing database round-trips and ensuring atomic message pairs.
    /// </summary>
    public Result<(Message UserMessage, Message AssistantMessage), Error> AppendMessageExchange(
        MessageContent userContent,
        MessageContent assistantContent,
        AiResponseId aiResponseId,
        TimeProvider timeProvider)
    {
        try
        {
            // Validate preconditions for both messages
            ValidateMessageAppendPreconditions(userContent.Value, MessageRole.User);
            CheckRule(new ConversationCanAcceptMoreMessagesRule(GetMessageCount()));
            CheckRule(new MessageContentMeetsDomainStandardsRule(userContent.Value));
            
            // Check we can add both messages
            CheckRule(new ConversationCanAcceptMoreMessagesRule(GetMessageCount() + 1));
            CheckRule(new AssistantResponseContentValidRule(assistantContent.Value));
            CheckRule(new AiResponseIdMustBeUniqueRule(aiResponseId, _messages));

            // Check for existing assistant message (idempotency)
            var existing = FindExistingMessageByAiResponseId(aiResponseId);
            if (existing is not null)
            {
                // Find the user message that should precede it
                var userMsg = GetMessageBySequence(existing.Sequence - 1);
                if (userMsg != null && userMsg.Role == MessageRole.User)
                    return Result.Success<(Message, Message), Error>((userMsg, existing));
            }

            var now = timeProvider.GetUtcNow();
            
            // Add user message
            var userMessage = CreateAndAddUserMessage(userContent, timeProvider);
            RaiseUserMessageEvent(userMessage, userContent.Value, now);
            
            // Add assistant message
            var assistantMessage = CreateAndAddAssistantMessage(assistantContent, aiResponseId, timeProvider);
            RaiseAssistantMessageEvent(assistantMessage, assistantContent.Value, aiResponseId, now);
            
            CheckRule(new MessageSequenceIntegrityRule(_messages));
            
            return Result.Success<(Message, Message), Error>((userMessage, assistantMessage));
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<(Message, Message), Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Imports a history of messages in bulk.
    /// Useful for migrating conversations or loading from external sources.
    /// </summary>
    public Result<IReadOnlyList<Message>, Error> ImportMessageHistory(
        IReadOnlyList<(MessageContent content, MessageRole role, AiResponseId? aiResponseId)> messages,
        TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            
            // Validate we're not exceeding limits
            var totalMessages = GetMessageCount() + messages.Count;
            CheckRule(new ConversationMessageLimitRule(totalMessages));
            
            // Validate turn-taking for the entire batch
            var currentRole = _messages.LastOrDefault()?.Role;
            foreach (var (content, role, _) in messages)
            {
                if (currentRole.HasValue && currentRole.Value == role)
                {
                    return Result.Failure<IReadOnlyList<Message>, Error>(
                        Error.Validation("Invalid message sequence - consecutive messages with same role", "CHAT010"));
                }
                currentRole = role;
            }

            var importedMessages = new List<Message>();
            var now = timeProvider.GetUtcNow();
            
            foreach (var (content, role, aiResponseId) in messages)
            {
                var sequence = GetMessageCount() + 1;
                Message message;
                
                if (role == MessageRole.User)
                {
                    message = Message.CreateUserMessage(Id, content, sequence);
                    _messages.Add(message);
                    RaiseUserMessageEvent(message, content.Value, now);
                }
                else
                {
                    if (!aiResponseId.HasValue)
                    {
                        return Result.Failure<IReadOnlyList<Message>, Error>(
                            Error.Validation("Assistant messages must have an AiResponseId", "CHAT011"));
                    }
                    
                    message = Message.CreateAssistantMessage(Id, content, sequence, aiResponseId.Value);
                    _messages.Add(message);
                    LastAiResponseId = aiResponseId.Value;
                    RaiseAssistantMessageEvent(message, content.Value, aiResponseId.Value, now);
                }
                
                importedMessages.Add(message);
            }
            
            MarkUpdated(timeProvider);
            
            RaiseDomainEvent(new ConversationHistoryImportedEvent(
                Id, importedMessages.Count, now));
            
            CheckRule(new MessageSequenceIntegrityRule(_messages));
            
            return Result.Success<IReadOnlyList<Message>, Error>(importedMessages.AsReadOnly());
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<IReadOnlyList<Message>, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Gets statistics about the conversation.
    /// </summary>
    public ConversationStatistics GetStatistics()
    {
        var messageCount = GetMessageCount();
        var userMessageCount = _messages.Count(m => m.Role == MessageRole.User);
        var assistantMessageCount = _messages.Count(m => m.Role == MessageRole.Assistant);
        var averageMessageLength = messageCount > 0 
            ? _messages.Average(m => m.Content.Value.Length)
            : 0;
        
        return new ConversationStatistics(
            TotalMessages: messageCount,
            UserMessages: userMessageCount,
            AssistantMessages: assistantMessageCount,
            AverageMessageLength: averageMessageLength,
            LastActivity: UpdatedAt ?? CreatedAt,
            IsActive: IsActive
        );
    }

    /// <summary>
    /// Updates the conversation title with a user-provided value (non-empty).
    /// </summary>
    public Result<Unit, Error> UpdateTitle(
        string newTitleValue,
        TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new TitleUpdateMustBeValidRule(newTitleValue));

            var titleResult = ConversationTitle.Create(newTitleValue);
            if (titleResult.IsFailure)
                return Result.Failure<Unit, Error>(titleResult.Error);

            var newTitle = titleResult.Value.Value;
            
            // Only update and raise event if title actually changed
            if (Title != newTitle)
            {
                var now = timeProvider.GetUtcNow();
                Title = newTitle;
                MarkUpdated(timeProvider);

                RaiseDomainEvent(new ConversationTitleUpdatedEvent(Id, Title, now));
            }

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Completes the conversation, preventing further messages.
    /// </summary>
    public Result<Unit, Error> Complete(TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new CompletionRequiresAtLeastOneMessageRule(GetMessageCount()));

            var now = timeProvider.GetUtcNow();
            Status = ConversationStatus.Completed;
            MarkUpdated(timeProvider);

            RaiseDomainEvent(new ConversationCompletedEvent(Id, GetMessageCount(), now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>Checks if the conversation belongs to a specific user.</summary>
    public bool BelongsTo(AxonUserId axonAxonUserId) => OwnerId == axonAxonUserId;

    /// <summary>
    /// Validates that the conversation can be accessed by the specified user.
    /// Returns a Result to maintain consistency with other domain operations.
    /// </summary>
    public Result<Unit, Error> ValidateAccess(AxonUserId axonAxonUserId)
    {
        try
        {
            CheckRule(new ConversationMustBelongToOwnerRule(this, axonAxonUserId));
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.Forbidden(ex.Message, ex.Error.Code));
        }
    }

    // ---------- Internals ----------

    /// <summary>
    /// Sets the creation time to support test scenarios with controlled time.
    /// Used internally to override the default system time with test-provided time.
    /// </summary>
    private void SetCreatedAt(DateTimeOffset createdAt)
    {
        // Use the internal method designed for infrastructure to set timestamps
        SetCreatedAtInternal(createdAt);
    }

    private void ValidateMessageAppendPreconditions(string content, MessageRole role)
    {
        CheckRule(new ConversationMustBeActiveRule(Status));
        CheckRule(new MessageContentWithinLimitsRule(content));
        CheckRule(new ConversationMessageLimitRule(GetMessageCount()));
        CheckRule(new MessageTurnTakingRule(_messages, role));
    }

    private Message? FindExistingMessageByAiResponseId(AiResponseId aiResponseId)
        => _messages.FirstOrDefault(m => m.AiResponseId?.Equals(aiResponseId) == true);

    private Message CreateAndAddUserMessage(MessageContent content, TimeProvider timeProvider)
    {
        var sequence = GetMessageCount() + 1;
        var message = Message.CreateUserMessage(Id, content, sequence);
        _messages.Add(message);
        MarkUpdated(timeProvider);
        return message;
    }

    private Message CreateAndAddAssistantMessage(MessageContent content, AiResponseId aiResponseId, TimeProvider timeProvider)
    {
        var sequence = GetMessageCount() + 1;
        var message = Message.CreateAssistantMessage(Id, content, sequence, aiResponseId);
        _messages.Add(message);
        LastAiResponseId = aiResponseId;
        MarkUpdated(timeProvider);
        return message;
    }

    private void RaiseUserMessageEvent(Message message, string content, DateTimeOffset now)
    {
        var preview = CreateContentPreview(content);
        RaiseDomainEvent(new UserMessageAppendedEvent(Id, message.Id, message.Sequence, preview, now));
    }

    private void RaiseAssistantMessageEvent(Message message, string content, AiResponseId aiResponseId, DateTimeOffset now)
    {
        var preview = CreateContentPreview(content);
        RaiseDomainEvent(new AssistantMessageAppendedEvent(Id, message.Id, message.Sequence, preview, aiResponseId, now));
    }

    private static string CreateContentPreview(string content)
        => TextSlices.Preview(content, ChatPrimitiveConstants.ConversationDefault.ContentPreviewLength);

    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}
