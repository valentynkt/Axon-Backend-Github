using System.Linq.Expressions;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Specification that determines if a message can be processed
/// </summary>
public sealed class MessageCanBeProcessedSpecification : Specification<Message>
{
    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message => 
            message.Status == MessageStatus.Draft &&
            !string.IsNullOrWhiteSpace(message.Content);
    }
}

/// <summary>
/// Specification that determines if a message is in a processable state
/// </summary>
public sealed class MessageIsProcessableSpecification : Specification<Message>
{
    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message =>
            message.Status == MessageStatus.Draft ||
            message.Status == MessageStatus.Processing;
    }
}

/// <summary>
/// Specification that determines if a message has completed processing
/// </summary>
public sealed class MessageProcessingCompletedSpecification : Specification<Message>
{
    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message =>
            message.Status == MessageStatus.Completed ||
            message.Status == MessageStatus.Failed ||
            message.Status == MessageStatus.Cancelled;
    }
}

/// <summary>
/// Specification that determines if a message is from a user
/// </summary>
public sealed class UserMessageSpecification : Specification<Message>
{
    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message => message.Role == MessageRole.User;
    }
}

/// <summary>
/// Specification that determines if a message is from an assistant
/// </summary>
public sealed class AssistantMessageSpecification : Specification<Message>
{
    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message => message.Role == MessageRole.Assistant;
    }
}

/// <summary>
/// Specification that determines if a message was created within a time range
/// </summary>
public sealed class MessageCreatedWithinTimeRangeSpecification : Specification<Message>
{
    private readonly DateTime _startTime;
    private readonly DateTime _endTime;

    public MessageCreatedWithinTimeRangeSpecification(DateTime startTime, DateTime endTime)
    {
        _startTime = startTime;
        _endTime = endTime;
    }

    public override Expression<Func<Message, bool>> ToExpression()
    {
        return message => 
            message.CreatedAt >= _startTime && 
            message.CreatedAt <= _endTime;
    }
}

/// <summary>
/// Specification that determines if a message has specific metadata
/// </summary>
public sealed class MessageHasMetadataSpecification : Specification<Message>
{
    private readonly string _key;
    private readonly string? _value;

    public MessageHasMetadataSpecification(string key, string? value = null)
    {
        _key = key;
        _value = value;
    }

    public override Expression<Func<Message, bool>> ToExpression()
    {
        if (_value == null)
        {
            return message => message.Metadata.ContainsKey(_key);
        }

        return message => 
            message.Metadata.ContainsKey(_key) && 
            message.Metadata[_key] == _value;
    }
}