using System.Text;
using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Domain service implementation for building conversation context with business rules
/// Following SPARC architecture pattern for domain services
/// </summary>
public sealed class ConversationContextBuilder : IConversationContextBuilder
{
    private const double DefaultTokensPerChar = 0.25; // Approximate tokens per character
    private const int DefaultMaxMessages = 50; // Default message limit to prevent memory issues

    /// <summary>
    /// Builds conversation context for AI processing with domain logic
    /// </summary>
    public Result<ConversationContext> BuildContext(Conversation conversation, int? maxMessages = null)
    {
        if (conversation is null)
            return Error.Validation("Conversation cannot be null");

        var options = new ContextBuildingOptions
        {
            MaxMessages = maxMessages ?? DefaultMaxMessages,
            IncludeSystemMessages = true,
            IncludeToolMessages = true
        };

        return BuildContextWithOptions(conversation, options);
    }

    /// <summary>
    /// Builds conversation context optimized for AI processing with token limits
    /// </summary>
    public Result<ConversationContext> BuildContextForAi(
        Conversation conversation, 
        int maxTokens, 
        double estimatedTokensPerChar = DefaultTokensPerChar)
    {
        if (conversation is null)
            return Error.Validation("Conversation cannot be null");

        if (maxTokens <= 0)
            return Error.Validation("MaxTokens must be greater than zero");

        if (estimatedTokensPerChar <= 0)
            return Error.Validation("EstimatedTokensPerChar must be greater than zero");

        var options = ContextBuildingOptions.ForAi(maxTokens) with
        {
            EstimatedTokensPerChar = estimatedTokensPerChar
        };

        return BuildContextWithOptions(conversation, options);
    }

    /// <summary>
    /// Builds context with specific message filtering and priorities
    /// </summary>
    public Result<ConversationContext> BuildContextWithOptions(
        Conversation conversation, 
        ContextBuildingOptions options)
    {
        if (conversation is null)
            return Error.Validation("Conversation cannot be null");

        if (options is null)
            return Error.Validation("ContextBuildingOptions cannot be null");

        try
        {
            var messages = GetFilteredMessages(conversation, options);
            var contextBuilder = new StringBuilder();
            var totalTokens = 0;
            var includedMessages = 0;
            var isTruncated = false;

            // Add conversation title if available
            if (!string.IsNullOrWhiteSpace(conversation.Title))
            {
                var titleLine = $"Conversation: {conversation.Title}";
                contextBuilder.AppendLine(titleLine);
                totalTokens += EstimateTokens(titleLine, options.EstimatedTokensPerChar);
            }

            // Process messages based on order preference
            var messagesToProcess = options.ReverseOrder 
                ? messages.Reverse().ToList() 
                : messages.ToList();

            foreach (var message in messagesToProcess)
            {
                var messageText = FormatMessage(message);
                var messageTokens = EstimateTokens(messageText, options.EstimatedTokensPerChar);

                // Check token limit if specified
                if (options.MaxTokens.HasValue)
                {
                    if (totalTokens + messageTokens > options.MaxTokens.Value)
                    {
                        isTruncated = true;
                        break;
                    }
                }

                // Check message limit if specified
                if (options.MaxMessages.HasValue && includedMessages >= options.MaxMessages.Value)
                {
                    isTruncated = true;
                    break;
                }

                contextBuilder.AppendLine(messageText);
                totalTokens += messageTokens;
                includedMessages++;
            }

            var formattedContext = contextBuilder.ToString().Trim();

            return ConversationContext.Create(
                conversation.Id,
                conversation.Title,
                formattedContext,
                includedMessages,
                totalTokens,
                isTruncated);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation($"Invalid argument for conversation context: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Error.Validation($"Invalid operation building conversation context: {ex.Message}");
        }
    }

    private static IEnumerable<Message> GetFilteredMessages(
        Conversation conversation, 
        ContextBuildingOptions options)
    {
        var messages = conversation.MessagesOrdered.AsEnumerable();

        // Filter by role if specified
        if (options.FilterByRole.HasValue)
        {
            messages = messages.Where(m => m.Role == options.FilterByRole.Value);
        }

        // Filter by message type preferences
        if (!options.IncludeSystemMessages)
        {
            messages = messages.Where(m => !m.Role.IsSystem);
        }

        if (!options.IncludeToolMessages)
        {
            messages = messages.Where(m => !m.Role.IsTool);
        }

        return messages;
    }

    private static string FormatMessage(Message message)
    {
        // Format: "Role: Content"
        var roleLabel = message.Role.Value.ToUpperInvariant();
        var content = message.Content?.Trim() ?? string.Empty;
        
        // Truncate very long messages to prevent context explosion
        if (content.Length > 2000)
        {
            content = $"{content[..2000]}...";
        }

        return $"{roleLabel}: {content}";
    }

    private static int EstimateTokens(string text, double tokensPerChar)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Simple token estimation based on character count
        // This is a rough approximation - actual tokenization would be more accurate
        return (int)Math.Ceiling(text.Length * tokensPerChar);
    }
}