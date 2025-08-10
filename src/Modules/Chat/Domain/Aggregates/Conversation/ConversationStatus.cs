namespace Axon.Modules.Chat.Domain.Aggregates.Conversation;

/// <summary>
/// Represents the status of a conversation.
/// MVP supports Active and Completed states only.
/// </summary>
public enum ConversationStatus
{
    Active = 1,
    Completed = 2
}