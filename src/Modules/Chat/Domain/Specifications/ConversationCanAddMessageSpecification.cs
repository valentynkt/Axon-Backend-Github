using System.Linq.Expressions;
using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Specifications;

/// <summary>
/// Specification that determines if a conversation can accept a new message
/// </summary>
public sealed class ConversationCanAddMessageSpecification : Specification<Conversation>
{
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return conversation => 
            conversation.IsActive &&
            (conversation.Context.MaintainFullHistory || 
             conversation.MessageCount < conversation.Context.MaxMessages);
    }
}

/// <summary>
/// Specification that determines if a conversation has reached its message limit
/// </summary>
public sealed class ConversationMessageLimitReachedSpecification : Specification<Conversation>
{
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return conversation => 
            !conversation.Context.MaintainFullHistory &&
            conversation.MessageCount >= conversation.Context.MaxMessages;
    }
}

/// <summary>
/// Specification that determines if a conversation is within its configured limits
/// </summary>
public sealed class ConversationWithinLimitsSpecification : Specification<Conversation>
{
    public override Expression<Func<Conversation, bool>> ToExpression()
    {
        return conversation =>
            (conversation.Context.MaintainFullHistory ||
             (conversation.MessageCount <= conversation.Context.MaxMessages &&
              conversation.ToolExecutionCount <= conversation.Context.MaxToolExecutions));
    }
}