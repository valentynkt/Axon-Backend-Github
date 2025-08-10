using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents a message role with validation rules.
/// - Only allows "user" and "assistant" roles (case-insensitive)
/// - Provides convenience properties for role checking
/// </summary>
public sealed record MessageRole : ValueObject
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";
    
    public string Value { get; }
    public bool IsUser => string.Equals(Value, UserRole, StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Value, AssistantRole, StringComparison.OrdinalIgnoreCase);

    private MessageRole(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Predefined User role
    /// </summary>
    public static MessageRole User => new(UserRole);

    /// <summary>
    /// Predefined Assistant role
    /// </summary>
    public static MessageRole Assistant => new(AssistantRole);

    /// <summary>
    /// Creates a MessageRole from string with case-insensitive validation.
    /// </summary>
    /// <param name="value">The role value to validate</param>
    /// <returns>Success with MessageRole or failure with validation error</returns>
    public static Result<MessageRole> FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<MessageRole>.Failure(
                Error.Validation("Invalid message role (allowed: user, assistant)", "CHAT.ROLE.INVALID"));
        }

        var trimmedValue = value.Trim();

        // Check if it's a valid role (case-insensitive)
        if (string.Equals(trimmedValue, UserRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MessageRole>.Success(new MessageRole(UserRole));
        }

        if (string.Equals(trimmedValue, AssistantRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MessageRole>.Success(new MessageRole(AssistantRole));
        }

        return Result<MessageRole>.Failure(
            Error.Validation("Invalid message role (allowed: user, assistant)", "CHAT.ROLE.INVALID"));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();

        if (!IsUser && !IsAssistant)
        {
            errors.Add(Error.Validation("Invalid message role (allowed: user, assistant)", "CHAT.ROLE.INVALID"));
        }

        return errors.Count == 0 
            ? Validation<Unit>.Valid(Unit.Value)
            : Validation<Unit>.Invalid(errors);
    }

    public override string ToString() => Value;
}