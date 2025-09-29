using System.Buffers;
using System.Text.RegularExpressions;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Domain.Services;

/// <summary>
/// Implementation of conversation title generation service.
/// </summary>
public sealed partial class ConversationTitleGenerator : IConversationTitleGenerator
{
    private const int MaxTitleLength = 200;
    private const int MinTitleLength = 3;
    private const int PreviewMessageCount = 3;
    private static readonly SearchValues<char> SentenceEndChars = SearchValues.Create(['.', '!', '?']);
    
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    public Result<string, Error> GenerateTitleFromMessages(IReadOnlyList<Message> messages)
    {
        if (!messages.Any())
        {
            return Result.Failure<string, Error>(
                Error.Validation("Cannot generate title from empty message list", "CHAT014"));
        }

        // Take the first user message as the basis for the title
        var firstUserMessage = messages
            .Where(m => m.Role == MessageRole.User)
            .OrderBy(m => m.Sequence)
            .FirstOrDefault();

        if (firstUserMessage == null)
        {
            return Result.Failure<string, Error>(
                Error.Validation("No user messages found to generate title from", "CHAT015"));
        }

        var content = firstUserMessage.Content.Value;
        
        // Generate a title from the first sentence or line
        var titleCandidate = ExtractTitleFromContent(content);
        
        // Validate and sanitize
        var sanitized = SanitizeTitle(titleCandidate);
        
        var validationResult = ValidateTitle(sanitized);
        if (validationResult.IsFailure)
            return Result.Failure<string, Error>(validationResult.Error);

        return Result.Success<string, Error>(sanitized);
    }

    public Result<Unit, Error> ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Unit, Error>(
                Error.Validation("Title cannot be empty", "CHAT016"));
        }

        if (title.Length < MinTitleLength)
        {
            return Result.Failure<Unit, Error>(
                Error.Validation($"Title must be at least {MinTitleLength} characters", "CHAT017"));
        }

        if (title.Length > MaxTitleLength)
        {
            return Result.Failure<Unit, Error>(
                Error.Validation($"Title cannot exceed {MaxTitleLength} characters", "CHAT018"));
        }

        return Result.Success<Unit, Error>(Unit.Value);
    }

    public string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Trim whitespace
        var sanitized = title.Trim();

        // Remove multiple spaces using compiled regex
        sanitized = MultipleSpacesRegex().Replace(sanitized, " ");

        // Truncate if too long using span-based approach
        if (sanitized.Length > MaxTitleLength)
        {
            sanitized = string.Concat(sanitized.AsSpan(0, MaxTitleLength - 3), "...");
        }

        return sanitized;
    }

    private static string ExtractTitleFromContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "New Conversation";

        // Take the first line or sentence
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Any())
        {
            var firstLine = lines[0];
            
            // If it's a question, use it as is (up to length limit)
            if (firstLine.EndsWith('?'))
            {
                return firstLine.Length > MaxTitleLength 
                    ? string.Concat(firstLine.AsSpan(0, MaxTitleLength - 3), "...")
                    : firstLine;
            }

            // Otherwise, take the first sentence
            var sentenceEnd = firstLine.AsSpan().IndexOfAny(SentenceEndChars);
            if (sentenceEnd > 0 && sentenceEnd < MaxTitleLength)
            {
                return firstLine[..(sentenceEnd + 1)];
            }

            // If no sentence end, just use the first line up to max length
            return firstLine.Length > MaxTitleLength
                ? string.Concat(firstLine.AsSpan(0, MaxTitleLength - 3), "...")
                : firstLine;
        }

        return "New Conversation";
    }
}