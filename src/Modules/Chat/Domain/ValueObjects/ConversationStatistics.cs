namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Statistics about a conversation.
/// </summary>
public record ConversationStatistics(
    int TotalMessages,
    int UserMessages,
    int AssistantMessages,
    double AverageMessageLength,
    DateTimeOffset LastActivity,
    bool IsActive
);