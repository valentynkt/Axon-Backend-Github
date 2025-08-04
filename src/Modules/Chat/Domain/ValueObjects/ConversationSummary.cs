using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Value object representing a conversation summary for AI context building
/// </summary>
public sealed record ConversationSummary
{
    public ConversationId Id { get; init; }
    public string Title { get; init; }
    public string Status { get; init; }
    public int MessageCount { get; init; }
    public string FirstMessage { get; init; }
    public string LastMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    private ConversationSummary(
        ConversationId id,
        string title,
        string status,
        int messageCount,
        string firstMessage,
        string lastMessage,
        DateTime createdAt,
        DateTime? completedAt)
    {
        Id = id;
        Title = title;
        Status = status;
        MessageCount = messageCount;
        FirstMessage = firstMessage;
        LastMessage = lastMessage;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Creates a conversation summary following SPARC factory pattern
    /// </summary>
    public static ConversationSummary Create(
        ConversationId id,
        string title,
        string status,
        int messageCount,
        string firstMessage,
        string lastMessage,
        DateTime createdAt,
        DateTime? completedAt = null)
    {
        return new ConversationSummary(
            id,
            title ?? string.Empty,
            status ?? string.Empty,
            messageCount,
            firstMessage ?? string.Empty,
            lastMessage ?? string.Empty,
            createdAt,
            completedAt);
    }

    /// <summary>
    /// Gets a formatted summary for AI context
    /// </summary>
    public string GetFormattedSummary()
    {
        var summary = $"Conversation: {Title} (Status: {Status}, Messages: {MessageCount})";
        
        if (MessageCount > 0)
        {
            summary += $"\nFirst message: {TruncateMessage(FirstMessage)}";
            if (FirstMessage != LastMessage)
            {
                summary += $"\nLast message: {TruncateMessage(LastMessage)}";
            }
        }

        return summary;
    }

    /// <summary>
    /// Checks if the conversation is active
    /// </summary>
    public bool IsActive => Status == "Active";

    /// <summary>
    /// Checks if the conversation is completed
    /// </summary>
    public bool IsCompleted => Status == "Completed";

    /// <summary>
    /// Checks if the conversation is archived
    /// </summary>
    public bool IsArchived => Status == "Archived";

    private static string TruncateMessage(string message, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message.Length <= maxLength 
            ? message 
            : $"{message[..maxLength]}...";
    }
}