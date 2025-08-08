using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.Entities;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule ensuring a conversation has a valid owner.
/// </summary>
public sealed record ConversationMustHaveOwnerRule : BusinessRule
{
    private readonly UserId? _ownerId;

    public ConversationMustHaveOwnerRule(UserId? ownerId)
    {
        _ownerId = ownerId;
    }

    public override string Code => "CONVERSATION_MUST_HAVE_OWNER";
    public override string Message => "Conversation must have a valid owner";

    public override bool IsBroken() => 
        _ownerId == null || string.IsNullOrWhiteSpace(_ownerId.Value);
}

/// <summary>
/// Business rule enforcing message limit per conversation to prevent abuse.
/// </summary>
public sealed record ConversationMessageLimitRule : BusinessRule
{
    private readonly int _currentMessageCount;
    private readonly int _maxMessages;

    public ConversationMessageLimitRule(int currentMessageCount, int maxMessages = 10000)
    {
        _currentMessageCount = currentMessageCount;
        _maxMessages = maxMessages;
    }

    public override string Code => "CONVERSATION_MESSAGE_LIMIT_EXCEEDED";
    public override string Message => $"Conversation cannot exceed {_maxMessages} messages";

    public override bool IsBroken() => _currentMessageCount >= _maxMessages;
}

/// <summary>
/// Business rule ensuring conversation title meets requirements.
/// </summary>
public sealed record ConversationTitleValidationRule : BusinessRule
{
    private readonly string _title;

    public ConversationTitleValidationRule(string title)
    {
        _title = title ?? string.Empty;
    }

    public override string Code => "CONVERSATION_INVALID_TITLE";
    public override string Message => "Conversation title must be between 1 and 200 characters";

    public override bool IsBroken() => 
        string.IsNullOrWhiteSpace(_title) || _title.Length > 200;
}

/// <summary>
/// Business rule ensuring only the conversation owner can modify it.
/// </summary>
public sealed record ConversationOwnershipRule : BusinessRule
{
    private readonly UserId _conversationOwnerId;
    private readonly UserId _requestingUserId;

    public ConversationOwnershipRule(UserId conversationOwnerId, UserId requestingUserId)
    {
        _conversationOwnerId = conversationOwnerId;
        _requestingUserId = requestingUserId;
    }

    public override string Code => "CONVERSATION_ACCESS_DENIED";
    public override string Message => "Only the conversation owner can modify this conversation";

    public override bool IsBroken() => 
        !_conversationOwnerId.Value.Equals(_requestingUserId.Value, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Business rule ensuring conversation is in the correct state for operations.
/// </summary>
public sealed record ConversationMustBeActiveRule : BusinessRule
{
    private readonly ConversationStatus _status;

    public ConversationMustBeActiveRule(ConversationStatus status)
    {
        _status = status;
    }

    public override string Code => "CONVERSATION_NOT_ACTIVE";
    public override string Message => "Conversation must be active to perform this operation";

    public override bool IsBroken() => _status != ConversationStatus.Active;
}

/// <summary>
/// Business rule ensuring message sequence integrity within a conversation.
/// </summary>
public sealed record MessageSequenceIntegrityRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;

    public MessageSequenceIntegrityRule(IReadOnlyList<Message> messages)
    {
        _messages = messages;
    }

    public override string Code => "MESSAGE_SEQUENCE_VIOLATION";
    public override string Message => "Message sequence must be consecutive starting from 1";

    public override bool IsBroken()
    {
        for (int i = 0; i < _messages.Count; i++)
        {
            var expectedSequence = i + 1;
            if (_messages[i].Sequence != expectedSequence)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Business rule validating message content requirements.
/// </summary>
public sealed record MessageContentValidationRule : BusinessRule
{
    private readonly string _content;

    public MessageContentValidationRule(string content)
    {
        _content = content ?? string.Empty;
    }

    public override string Code => "MESSAGE_INVALID_CONTENT";
    public override string Message => "Message content must be between 1 and 100,000 characters";

    public override bool IsBroken() => 
        string.IsNullOrWhiteSpace(_content) || _content.Length > 100_000;
}

/// <summary>
/// Business rule preventing duplicate messages in rapid succession.
/// </summary>
public sealed record DuplicateMessagePreventionRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _existingMessages;
    private readonly string _newContent;
    private readonly TimeSpan _duplicateWindow;

    public DuplicateMessagePreventionRule(
        IReadOnlyList<Message> existingMessages, 
        string newContent,
        TimeSpan? duplicateWindow = null)
    {
        _existingMessages = existingMessages;
        _newContent = newContent?.Trim() ?? string.Empty;
        _duplicateWindow = duplicateWindow ?? TimeSpan.FromSeconds(5);
    }

    public override string Code => "DUPLICATE_MESSAGE_DETECTED";
    public override string Message => "Duplicate message detected within the time window";

    public override bool IsBroken()
    {
        if (string.IsNullOrWhiteSpace(_newContent))
            return false;

        var recentMessages = _existingMessages
            .Where(m => DateTime.UtcNow - m.CreatedAt <= _duplicateWindow)
            .ToList();

        return recentMessages.Any(m => 
            string.Equals(m.Content.Trim(), _newContent, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Business rule enforcing rate limiting for message creation.
/// </summary>
public sealed record MessageRateLimitRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _recentMessages;
    private readonly int _maxMessagesPerMinute;
    private readonly TimeSpan _timeWindow;

    public MessageRateLimitRule(
        IReadOnlyList<Message> recentMessages, 
        int maxMessagesPerMinute = 60)
    {
        _recentMessages = recentMessages;
        _maxMessagesPerMinute = maxMessagesPerMinute;
        _timeWindow = TimeSpan.FromMinutes(1);
    }

    public override string Code => "MESSAGE_RATE_LIMIT_EXCEEDED";
    public override string Message => $"Cannot exceed {_maxMessagesPerMinute} messages per minute";

    public override bool IsBroken()
    {
        var cutoffTime = DateTime.UtcNow - _timeWindow;
        var recentMessageCount = _recentMessages.Count(m => m.CreatedAt >= cutoffTime);
        
        return recentMessageCount >= _maxMessagesPerMinute;
    }
}