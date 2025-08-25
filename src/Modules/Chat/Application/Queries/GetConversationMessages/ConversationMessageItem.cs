namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Read model for conversation messages with primitives only (no value objects).
/// Used for efficient data transfer from Application to API layer.
/// </summary>
public sealed record ConversationMessageItem(
    Guid MessageId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    int Sequence
);