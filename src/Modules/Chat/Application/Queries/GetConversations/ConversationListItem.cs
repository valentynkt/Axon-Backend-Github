namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Read model representing a conversation item in a list/grid view.
/// Optimized for display purposes with minimal data transfer.
/// </summary>
public sealed record ConversationListItem(
    Guid ConversationId,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? LastAssistantResponseId
);