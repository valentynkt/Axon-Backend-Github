using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service for building conversation context with business rules and constraints
/// Following SPARC architecture pattern for domain services
/// </summary>
public interface IConversationContextBuilder
{
    /// <summary>
    /// Builds conversation context for AI processing with domain logic
    /// </summary>
    /// <param name="conversation">The conversation to build context from</param>
    /// <param name="maxMessages">Maximum number of messages to include (optional)</param>
    /// <returns>Result containing the built context or error</returns>
    Result<ConversationContext> BuildContext(Conversation.Conversation conversation, int? maxMessages = null);

    /// <summary>
    /// Builds conversation context optimized for AI processing with token limits
    /// </summary>
    /// <param name="conversation">The conversation to build context from</param>
    /// <param name="maxTokens">Maximum number of tokens to include</param>
    /// <param name="estimatedTokensPerChar">Estimated tokens per character ratio</param>
    /// <returns>Result containing the AI-optimized context or error</returns>
    Result<ConversationContext> BuildContextForAi(
        Conversation.Conversation conversation, 
        int maxTokens, 
        double estimatedTokensPerChar = 0.25);

    /// <summary>
    /// Builds context with specific message filtering and priorities
    /// </summary>
    /// <param name="conversation">The conversation to build context from</param>
    /// <param name="options">Context building options</param>
    /// <returns>Result containing the filtered context or error</returns>
    Result<ConversationContext> BuildContextWithOptions(
        Conversation.Conversation conversation, 
        ContextBuildingOptions options);
}

/// <summary>
/// Value object representing built conversation context following SPARC pattern
/// </summary>
public sealed record ConversationContext
{
    public ConversationId ConversationId { get; init; }
    public string Title { get; init; }
    public string FormattedContext { get; init; }
    public int MessageCount { get; init; }
    public int EstimatedTokenCount { get; init; }
    public bool IsTruncated { get; init; }

    private ConversationContext(
        ConversationId conversationId,
        string title,
        string formattedContext,
        int messageCount,
        int estimatedTokenCount,
        bool isTruncated)
    {
        ConversationId = conversationId;
        Title = title;
        FormattedContext = formattedContext;
        MessageCount = messageCount;
        EstimatedTokenCount = estimatedTokenCount;
        IsTruncated = isTruncated;
    }

    /// <summary>
    /// Creates a conversation context following SPARC factory pattern
    /// </summary>
    public static Result<ConversationContext> Create(
        ConversationId conversationId,
        string title,
        string formattedContext,
        int messageCount,
        int estimatedTokenCount,
        bool isTruncated = false)
    {
        if (conversationId.Equals(default(ConversationId)))
            return Error.Validation("ConversationId cannot be default");

        title = title?.Trim() ?? string.Empty;
        formattedContext = formattedContext?.Trim() ?? string.Empty;

        if (messageCount < 0)
            return Error.Validation("Message count cannot be negative");

        if (estimatedTokenCount < 0)
            return Error.Validation("Estimated token count cannot be negative");

        return new ConversationContext(
            conversationId,
            title,
            formattedContext,
            messageCount,
            estimatedTokenCount,
            isTruncated);
    }
}

/// <summary>
/// Options for context building following SPARC configuration pattern
/// </summary>
public sealed record ContextBuildingOptions
{
    public int? MaxMessages { get; init; }
    public int? MaxTokens { get; init; }
    public double EstimatedTokensPerChar { get; init; } = 0.25;
    public bool IncludeSystemMessages { get; init; } = true;
    public bool IncludeToolMessages { get; init; } = true;
    public MessageRole? FilterByRole { get; init; }
    public bool ReverseOrder { get; init; }

    /// <summary>
    /// Creates default context building options
    /// </summary>
    public static ContextBuildingOptions Default => new();

    /// <summary>
    /// Creates options optimized for AI processing
    /// </summary>
    public static ContextBuildingOptions ForAi(int maxTokens) => new()
    {
        MaxTokens = maxTokens,
        IncludeSystemMessages = true,
        IncludeToolMessages = false, // Reduce noise for AI
        ReverseOrder = false
    };

    /// <summary>
    /// Creates options for recent messages only
    /// </summary>
    public static ContextBuildingOptions Recent(int maxMessages) => new()
    {
        MaxMessages = maxMessages,
        IncludeSystemMessages = false,
        IncludeToolMessages = false,
        ReverseOrder = false
    };
}