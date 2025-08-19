using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Time;
using BuildingBlocks.Core.Abstractions.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Domain.Tests.EdgeCases.TestData;

/// <summary>
/// Test builder for creating conversations with various states and message counts.
/// </summary>
public class ConversationBuilder
{
    private UserId _ownerId = UserId.New();
    private string? _title = null;
    private IClock _clock = new FixedClock(DateTimeOffset.UtcNow);
    private readonly List<(MessageRole role, string content)> _messages = new();

    public ConversationBuilder WithOwner(UserId ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public ConversationBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    public ConversationBuilder WithClock(IClock clock)
    {
        _clock = clock;
        return this;
    }

    public ConversationBuilder WithUserMessage(string content)
    {
        _messages.Add((MessageRole.User, content));
        return this;
    }

    public ConversationBuilder WithAssistantMessage(string content)
    {
        _messages.Add((MessageRole.Assistant, content));
        return this;
    }

    public ConversationBuilder WithMessages(int count, MessageRole role = null!)
    {
        role ??= MessageRole.User;
        for (int i = 0; i < count; i++)
        {
            _messages.Add((role, $"Message {i + 1}"));
        }
        return this;
    }

    public ConversationBuilder WithAlternatingMessages(int totalCount)
    {
        for (int i = 0; i < totalCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            _messages.Add((role, $"Message {i + 1}"));
        }
        return this;
    }

    public Result<Conversation> Build()
    {
        var conversationResult = Conversation.Start(_ownerId, _title, _clock);
        if (conversationResult.IsFailure)
            return conversationResult;

        var conversation = conversationResult.Value;

        // Add messages
        foreach (var (role, content) in _messages)
        {
            var contentResult = MessageContent.Create(content);
            if (contentResult.IsFailure)
                return Result<Conversation>.Failure(contentResult.Error);

            Result<Message> messageResult = role.IsUser 
                ? conversation.AppendUserMessage(contentResult.Value, _clock)
                : conversation.AppendAssistantMessage(contentResult.Value, _clock);

            if (messageResult.IsFailure)
                return Result<Conversation>.Failure(messageResult.Error);
        }

        return Result<Conversation>.Success(conversation);
    }

    public static ConversationBuilder New() => new();
}