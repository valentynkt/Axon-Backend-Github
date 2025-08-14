using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents message content with validation rules.
/// - Cannot be null or empty (after trimming)
/// - Maximum length of 100,000 characters
/// - Provides preview functionality
/// </summary>
public sealed record MessageContent : ValueObject
{
    private const int MaxLength = 100_000;
    
    public string Value { get; }
    public int Length => Value.Length;

    private MessageContent(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a MessageContent with validation.
    /// </summary>
    /// <param name="value">The content value to validate</param>
    /// <returns>Success with MessageContent or failure with validation error</returns>
    public static Result<MessageContent> Create(string? value)
    {
        if (value is null)
        {
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot be null", "CHAT.MESSAGE.NULL"));
        }

        // Trim whitespace and check if empty
        var trimmedValue = value.Trim();
        
        if (string.IsNullOrEmpty(trimmedValue))
        {
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot be empty.", "CHAT_MESSAGE_CONTENT_EMPTY"));
        }

        // Check maximum length
        if (trimmedValue.Length > MaxLength)
        {
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot exceed 100000 characters.", "CHAT_MESSAGE_CONTENT_TOO_LONG"));
        }

        return Result<MessageContent>.Success(new MessageContent(trimmedValue));
    }

    /// <summary>
    /// Returns a preview of the message content up to the specified maximum length.
    /// Does not add ellipsis - just returns substring.
    /// </summary>
    /// <param name="max">Maximum length of preview (default: 100)</param>
    /// <returns>Preview string</returns>
    public string Preview(int max = 100)
    {
        if (max <= 0)
            return string.Empty;

        return Value.Length <= max ? Value : Value[..max];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override ValidationResult<Unit> Validate()
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(Value))
        {
            errors.Add(Error.Validation("Message content cannot be empty.", "CHAT_MESSAGE_CONTENT_EMPTY"));
        }
        else if (Value.Length > MaxLength)
        {
            errors.Add(Error.Validation("Message content cannot exceed 100000 characters.", "CHAT_MESSAGE_CONTENT_TOO_LONG"));
        }

        return errors.Count == 0 
            ? Validation.Valid(Unit.Value)
            : Validation.Invalid<Unit>(errors);
    }

    public override string ToString() => Value;
}