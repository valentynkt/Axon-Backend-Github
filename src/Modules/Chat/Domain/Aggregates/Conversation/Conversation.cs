// /Axon/Modules/Chat/Domain/Aggregates/Conversation/Conversation.cs
#nullable enable
using System.Linq;
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

// Queryable, read-only navigation for EF/specs.
// Mutations still go through aggregate methods; EF maps the backing field.
    public IReadOnlyCollection<Message> Messages => _messages;

    // Core state
    public UserId OwnerId { get; private set; }
    public ConversationStatus Status { get; private set; }
    public string? Title { get; private set; }

    /// <summary>
    /// The anchor for the next model call (use as 'previous_response_id').
    /// Set only when an assistant message is appended successfully.
    /// </summary>
    public AiResponseId? LastAiResponseId { get; private set; }

    // Computed
    public int MessageCount => _messages.Count;
    public IReadOnlyList<Message> MessagesOrdered => _messages.AsReadOnly();

    // Helpers
    public bool IsActive => Status == ConversationStatus.Active;
    public bool HasDefaultTitle => Title is null;

    // EF Core parameterless ctor
    private Conversation() : base() { }

    private Conversation(ConversationId id, UserId ownerId, string? title)
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
        UserId ownerId,
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
    /// Appends a user message to the conversation.
    /// </summary>
    public Result<Message, Error> AppendUserMessageToConversation(
        MessageContent content,
        TimeProvider timeProvider)
    {
        try
        {
            ValidateMessageAppendPreconditions(content.Value, MessageRole.User);
            CheckRule(new ConversationCanAcceptMoreMessagesRule(_messages.Count));
            CheckRule(new MessageContentMeetsDomainStandardsRule(content.Value));

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddUserMessage(content);

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
            CheckRule(new ConversationCanAcceptMoreMessagesRule(_messages.Count));
            CheckRule(new AssistantResponseContentValidRule(content.Value));
            CheckRule(new AiResponseIdMustBeUniqueRule(aiResponseId, _messages));

            // Idempotency: if message with same AI response id exists, return it
            var existing = FindExistingMessageByAiResponseId(aiResponseId);
            if (existing is not null)
                return Result.Success<Message, Error>(existing);

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddAssistantMessage(content, aiResponseId);

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
                MarkUpdated();

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
            CheckRule(new CompletionRequiresAtLeastOneMessageRule(MessageCount));

            var now = timeProvider.GetUtcNow();
            Status = ConversationStatus.Completed;
            MarkUpdated();

            RaiseDomainEvent(new ConversationCompletedEvent(Id, MessageCount, now));

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Unit, Error>(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>Checks if the conversation belongs to a specific user.</summary>
    public bool BelongsTo(UserId userId) => OwnerId == userId;

    // ---------- Internals ----------

    private void ValidateMessageAppendPreconditions(string content, MessageRole role)
    {
        CheckRule(new ConversationMustBeActiveRule(Status));
        CheckRule(new MessageContentWithinLimitsRule(content));
        CheckRule(new ConversationMessageLimitRule(MessageCount));
        CheckRule(new MessageTurnTakingRule(_messages, role));
    }

    private Message? FindExistingMessageByAiResponseId(AiResponseId aiResponseId)
        => _messages.FirstOrDefault(m => m.AiResponseId?.Equals(aiResponseId) == true);

    private Message CreateAndAddUserMessage(MessageContent content)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateUserMessage(Id, content, sequence);
        _messages.Add(message);
        MarkUpdated();
        return message;
    }

    private Message CreateAndAddAssistantMessage(MessageContent content, AiResponseId aiResponseId)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateAssistantMessage(Id, content, sequence, aiResponseId);
        _messages.Add(message);
        LastAiResponseId = aiResponseId;
        MarkUpdated();
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
