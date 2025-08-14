using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents a conversation title with validation rules.
/// - Allows empty strings (MVP defaulting path)
/// - Trims leading/trailing whitespace while preserving internal whitespace and case
/// - Maximum length of 200 characters
/// </summary>
public sealed record ConversationTitle : ValueObject
{
    private const int MaxLength = 200;
    
    public string Value { get; }
    public int Length => Value.Length;
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    private ConversationTitle(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a ConversationTitle with validation.
    /// </summary>
    /// <param name="value">The title value to validate</param>
    /// <returns>Success with ConversationTitle or failure with validation error</returns>
    public static Result<ConversationTitle> Create(string? value)
    {
        if (value is null)
        {
            return Result<ConversationTitle>.Failure(
                Error.Validation("Title is invalid", "CHAT.TITLE.INVALID"));
        }

        // Trim leading/trailing whitespace but preserve internal whitespace and case
        var trimmedValue = value.Trim();

        // Empty string is allowed (MVP defaulting path)
        if (trimmedValue.Length == 0)
        {
            return Result<ConversationTitle>.Success(new ConversationTitle(trimmedValue));
        }

        // Check maximum length
        if (trimmedValue.Length > MaxLength)
        {
            return Result<ConversationTitle>.Failure(
                Error.Validation("Title cannot exceed 200 characters.", "CHAT_CONVERSATION_TITLE_TOO_LONG"));
        }

        return Result<ConversationTitle>.Success(new ConversationTitle(trimmedValue));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override ValidationResult<Unit> Validate()
    {
        var errors = new List<Error>();

        if (Value.Length > MaxLength)
        {
            errors.Add(Error.Validation("Title cannot exceed 200 characters.", "CHAT_CONVERSATION_TITLE_TOO_LONG"));
        }

        return errors.Count == 0 
            ? Validation.Valid(Unit.Value)
            : Validation.Invalid<Unit>(errors);
    }

    public override string ToString() => Value;
}