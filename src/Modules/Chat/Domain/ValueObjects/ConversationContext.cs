using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents the context information for a conversation
/// </summary>
public sealed record ConversationContext
{
    /// <summary>
    /// The maximum number of messages to keep in context
    /// </summary>
    public int MaxMessages { get; }

    /// <summary>
    /// The maximum number of tool executions to track
    /// </summary>
    public int MaxToolExecutions { get; }

    /// <summary>
    /// Whether the conversation should maintain full history
    /// </summary>
    public bool MaintainFullHistory { get; }

    /// <summary>
    /// Custom context metadata
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// The conversation's subject or topic
    /// </summary>
    public string? Subject { get; }

    /// <summary>
    /// Tags associated with the conversation
    /// </summary>
    public IReadOnlySet<string> Tags { get; }

    private ConversationContext(
        int maxMessages,
        int maxToolExecutions,
        bool maintainFullHistory,
        IReadOnlyDictionary<string, string> metadata,
        string? subject,
        IReadOnlySet<string> tags)
    {
        MaxMessages = maxMessages;
        MaxToolExecutions = maxToolExecutions;
        MaintainFullHistory = maintainFullHistory;
        Metadata = metadata;
        Subject = subject;
        Tags = tags;
    }

    /// <summary>
    /// Creates a default conversation context
    /// </summary>
    public static ConversationContext Default() => new(
        maxMessages: 100,
        maxToolExecutions: 50,
        maintainFullHistory: true,
        metadata: new Dictionary<string, string>(),
        subject: null,
        tags: new HashSet<string>());

    /// <summary>
    /// Creates a conversation context with custom settings
    /// </summary>
    public static Result<ConversationContext> Create(
        int maxMessages = 100,
        int maxToolExecutions = 50,
        bool maintainFullHistory = true,
        Dictionary<string, string>? metadata = null,
        string? subject = null,
        HashSet<string>? tags = null)
    {
        if (maxMessages <= 0)
            return Error.Validation("MaxMessages must be greater than zero");

        if (maxToolExecutions < 0)
            return Error.Validation("MaxToolExecutions cannot be negative");

        if (!string.IsNullOrWhiteSpace(subject) && subject.Length > 500)
            return Error.Validation("Subject cannot exceed 500 characters");

        var validatedMetadata = metadata ?? new Dictionary<string, string>();
        var validatedTags = tags ?? new HashSet<string>();

        // Validate metadata
        foreach (var (key, value) in validatedMetadata)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Error.Validation("Metadata keys cannot be null or empty");
            
            if (key.Length > 100)
                return Error.Validation("Metadata keys cannot exceed 100 characters");
                
            if (value?.Length > 1000)
                return Error.Validation("Metadata values cannot exceed 1000 characters");
        }

        // Validate tags
        foreach (var tag in validatedTags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return Error.Validation("Tags cannot be null or empty");
                
            if (tag.Length > 50)
                return Error.Validation("Tags cannot exceed 50 characters");
        }

        return new ConversationContext(
            maxMessages,
            maxToolExecutions,
            maintainFullHistory,
            validatedMetadata,
            subject?.Trim(),
            validatedTags);
    }

    /// <summary>
    /// Creates a new conversation context with updated subject
    /// </summary>
    public Result<ConversationContext> WithSubject(string? subject)
    {
        return Create(MaxMessages, MaxToolExecutions, MaintainFullHistory, 
            new Dictionary<string, string>(Metadata), subject, new HashSet<string>(Tags));
    }

    /// <summary>
    /// Creates a new conversation context with added tag
    /// </summary>
    public Result<ConversationContext> WithTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Error.Validation("Tag cannot be null or empty");

        var newTags = new HashSet<string>(Tags) { tag.Trim() };
        return Create(MaxMessages, MaxToolExecutions, MaintainFullHistory,
            new Dictionary<string, string>(Metadata), Subject, newTags);
    }

    /// <summary>
    /// Creates a new conversation context with added metadata
    /// </summary>
    public Result<ConversationContext> WithMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Error.Validation("Metadata key cannot be null or empty");

        var newMetadata = new Dictionary<string, string>(Metadata) { [key.Trim()] = value?.Trim() ?? string.Empty };
        return Create(MaxMessages, MaxToolExecutions, MaintainFullHistory,
            newMetadata, Subject, new HashSet<string>(Tags));
    }
}