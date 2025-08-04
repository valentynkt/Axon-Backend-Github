namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Value object representing conversation statistics following SPARC patterns
/// </summary>
public sealed record ConversationStatistics
{
    /// <summary>
    /// Total number of conversations
    /// </summary>
    public int TotalConversations { get; }

    /// <summary>
    /// Number of active conversations
    /// </summary>
    public int ActiveConversations { get; }

    /// <summary>
    /// Number of completed conversations
    /// </summary>
    public int CompletedConversations { get; }

    /// <summary>
    /// Number of archived conversations
    /// </summary>
    public int ArchivedConversations { get; }

    /// <summary>
    /// Total number of messages across all conversations
    /// </summary>
    public int TotalMessages { get; }

    /// <summary>
    /// Average messages per conversation
    /// </summary>
    public double AverageMessagesPerConversation { get; }

    /// <summary>
    /// Date of the oldest conversation
    /// </summary>
    public DateTime? FirstConversationDate { get; }

    /// <summary>
    /// Date of the most recent conversation
    /// </summary>
    public DateTime? LastConversationDate { get; }

    private ConversationStatistics(
        int totalConversations,
        int activeConversations,
        int completedConversations,
        int archivedConversations,
        int totalMessages,
        double averageMessagesPerConversation,
        DateTime? firstConversationDate,
        DateTime? lastConversationDate)
    {
        TotalConversations = totalConversations;
        ActiveConversations = activeConversations;
        CompletedConversations = completedConversations;
        ArchivedConversations = archivedConversations;
        TotalMessages = totalMessages;
        AverageMessagesPerConversation = averageMessagesPerConversation;
        FirstConversationDate = firstConversationDate;
        LastConversationDate = lastConversationDate;
    }

    /// <summary>
    /// Creates conversation statistics
    /// </summary>
    public static ConversationStatistics Create(
        int totalConversations,
        int activeConversations,
        int completedConversations,
        int archivedConversations,
        int totalMessages,
        DateTime? firstConversationDate = null,
        DateTime? lastConversationDate = null)
    {
        var averageMessages = totalConversations > 0 
            ? totalMessages / (double)totalConversations 
            : 0;

        return new ConversationStatistics(
            totalConversations,
            activeConversations,
            completedConversations,
            archivedConversations,
            totalMessages,
            averageMessages,
            firstConversationDate,
            lastConversationDate);
    }

    /// <summary>
    /// Creates empty statistics
    /// </summary>
    public static ConversationStatistics Empty() =>
        new(0, 0, 0, 0, 0, 0, null, null);
}