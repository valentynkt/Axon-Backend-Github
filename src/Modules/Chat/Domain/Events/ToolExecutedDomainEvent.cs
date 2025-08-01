using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a tool is executed within a conversation
/// </summary>
public sealed record ToolExecutedDomainEvent : DomainEvent
{
    /// <summary>
    /// The ID of the conversation where the tool was executed
    /// </summary>
    public ConversationId ConversationId { get; }

    /// <summary>
    /// The ID of the message that triggered the tool execution
    /// </summary>
    public MessageId MessageId { get; }

    /// <summary>
    /// The tool execution details
    /// </summary>
    public ToolExecution ToolExecution { get; }

    /// <summary>
    /// Whether the tool execution was successful
    /// </summary>
    public bool IsSuccess => ToolExecution.IsSuccess;

    /// <summary>
    /// The time taken to execute the tool
    /// </summary>
    public TimeSpan ExecutionTime => ToolExecution.ExecutionTime;

    public ToolExecutedDomainEvent(
        ConversationId conversationId,
        MessageId messageId,
        ToolExecution toolExecution)
    {
        ConversationId = conversationId;
        MessageId = messageId;
        ToolExecution = toolExecution;
    }
}