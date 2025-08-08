using BuildingBlocks.Core.Domain.ValueObjects;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

/// <summary>
/// Epic 2 enhanced message content value object with comprehensive validation and Epic 1 functional patterns.
/// Provides rich domain behavior with sanitization, validation, and business rule compliance.
/// </summary>
public sealed record MessageContent : ValueObject
{
    public const int MaxLength = 100_000;
    public const int MinLength = 1;
    
    public string Value { get; }
    public int Length => Value.Length;
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public bool IsValid => !IsEmpty && Length <= MaxLength;

    private MessageContent(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a MessageContent instance with comprehensive validation using Epic 2 patterns.
    /// Applies sanitization, length validation, and content validation rules.
    /// </summary>
    public static Result<MessageContent> Create(string content)
    {
        if (content == null)
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot be null", "MESSAGE_CONTENT_NULL"));

        // Sanitize input
        var sanitized = SanitizeContent(content);
        
        if (string.IsNullOrWhiteSpace(sanitized))
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot be empty after sanitization", "MESSAGE_CONTENT_EMPTY"));
                
        if (sanitized.Length > MaxLength)
            return Result<MessageContent>.Failure(
                Error.Validation($"Message content cannot exceed {MaxLength} characters", "MESSAGE_CONTENT_TOO_LONG"));

        var messageContent = new MessageContent(sanitized);
        
        // Validate using Epic 2 validation pattern
        var validation = messageContent.Validate();
        if (validation.IsInvalid)
            return validation.ToResultWithAggregatedError();

        return Result<MessageContent>.Success(messageContent);
    }

    /// <summary>
    /// Validates the message content using Epic 2 validation patterns.
    /// </summary>
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        // Content validation rules
        if (IsEmpty)
            errors.Add(Error.Validation("Message content cannot be empty", "MESSAGE_CONTENT_EMPTY"));
            
        if (Length > MaxLength)
            errors.Add(Error.Validation($"Message content exceeds maximum length of {MaxLength}", "MESSAGE_CONTENT_TOO_LONG"));
            
        // Content quality validation
        if (IsOnlyWhitespace())
            errors.Add(Error.Validation("Message content cannot contain only whitespace", "MESSAGE_CONTENT_WHITESPACE_ONLY"));
            
        if (ContainsSuspiciousContent())
            errors.Add(Error.Validation("Message content contains suspicious patterns", "MESSAGE_CONTENT_SUSPICIOUS"));
        
        return errors.Any() 
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }

    /// <summary>
    /// Sanitizes content by normalizing whitespace and removing potentially harmful content.
    /// </summary>
    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // Normalize line endings
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Trim leading/trailing whitespace but preserve internal structure
        normalized = normalized.Trim();
        
        // Normalize excessive whitespace (multiple spaces/tabs to single space)
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        
        // Remove null characters and other control characters (except newlines and tabs)
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        
        return normalized;
    }

    /// <summary>
    /// Checks if content is only whitespace characters.
    /// </summary>
    private bool IsOnlyWhitespace()
    {
        return Value.All(char.IsWhiteSpace);
    }

    /// <summary>
    /// Basic content validation for suspicious patterns.
    /// Can be extended with more sophisticated content filtering.
    /// </summary>
    private bool ContainsSuspiciousContent()
    {
        // Basic checks for excessive repetition
        if (HasExcessiveRepetition())
            return true;
            
        // Check for excessive length without spaces (potential spam)
        if (HasExcessiveConcatenation())
            return true;
            
        return false;
    }

    /// <summary>
    /// Detects excessive character repetition that might indicate spam.
    /// </summary>
    private bool HasExcessiveRepetition()
    {
        if (Length < 10) return false;

        // Check for more than 50% repeated characters
        var charGroups = Value.GroupBy(c => c);
        var mostCommonCharCount = charGroups.Max(g => g.Count());
        
        return (double)mostCommonCharCount / Length > 0.5;
    }

    /// <summary>
    /// Detects excessive concatenation without spaces.
    /// </summary>
    private bool HasExcessiveConcatenation()
    {
        if (Length < 100) return false;

        // Check for strings longer than 100 characters without spaces
        var words = Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => word.Length > 100);
    }

    /// <summary>
    /// Truncates content to specified length while preserving word boundaries.
    /// </summary>
    public MessageContent Truncate(int maxLength)
    {
        if (Length <= maxLength)
            return this;

        var truncated = Value.Substring(0, maxLength);
        
        // Try to preserve word boundaries
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.8) // Only if we don't lose too much content
        {
            truncated = truncated.Substring(0, lastSpace);
        }
        
        return new MessageContent(truncated.Trim());
    }

    /// <summary>
    /// Extracts a preview of the message for display purposes.
    /// </summary>
    public string GetPreview(int maxPreviewLength = 100)
    {
        if (Length <= maxPreviewLength)
            return Value;

        var preview = Value.Substring(0, maxPreviewLength);
        var lastSpace = preview.LastIndexOf(' ');
        
        if (lastSpace > maxPreviewLength * 0.7)
            preview = preview.Substring(0, lastSpace);
            
        return preview.Trim() + "...";
    }

    /// <summary>
    /// Gets basic metrics about the message content.
    /// </summary>
    public MessageContentMetrics GetMetrics()
    {
        var wordCount = Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var lineCount = Value.Count(c => c == '\n') + 1;
        var characterCount = Length;
        var characterCountWithoutSpaces = Value.Count(c => !char.IsWhiteSpace(c));

        return new MessageContentMetrics(
            WordCount: wordCount,
            LineCount: lineCount,
            CharacterCount: characterCount,
            CharacterCountWithoutSpaces: characterCountWithoutSpaces);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(MessageContent content) => content.Value;
    
    // Removed explicit cast operator to force use of Create method for validation
}

/// <summary>
/// Metrics about message content for analytics and display purposes.
/// </summary>
public sealed record MessageContentMetrics(
    int WordCount,
    int LineCount,
    int CharacterCount,
    int CharacterCountWithoutSpaces);