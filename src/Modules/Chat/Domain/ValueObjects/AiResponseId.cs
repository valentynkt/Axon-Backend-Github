using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using Axon.Modules.Chat.Domain.Constants;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Represents an external AI response identifier.
/// Encapsulates the identity of AI responses for idempotency and context linking.
/// Used to track OpenAI response IDs and maintain conversation continuity.
/// </summary>
public sealed record AiResponseId : ValueObject
{
    public const int MaxLength = ChatDomainConstants.AiResponseId.MaxLength;
    public const int MinLength = ChatDomainConstants.AiResponseId.MinLength;
    
    public string Value { get; }
    
    private AiResponseId(string value)
    {
        Value = value;
    }
    
    /// <summary>
    /// Creates an AiResponseId with validation.
    /// </summary>
    /// <param name="value">The AI response identifier value</param>
    /// <returns>Success with AiResponseId or failure with validation error</returns>
    public static Result<AiResponseId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<AiResponseId>.Failure(
                Error.Validation("AI response ID is required", "CHAT.AI.RESPONSE_ID.EMPTY"));
        }
        
        var trimmed = value.Trim();
        
        if (trimmed.Length < MinLength)
        {
            return Result<AiResponseId>.Failure(
                Error.Validation($"AI response ID must be at least {MinLength} characters", "CHAT.AI.RESPONSE_ID.TOO_SHORT"));
        }
        
        if (trimmed.Length > MaxLength)
        {
            return Result<AiResponseId>.Failure(
                Error.Validation($"AI response ID cannot exceed {MaxLength} characters", "CHAT.AI.RESPONSE_ID.TOO_LONG"));
        }
        
        return Result<AiResponseId>.Success(new AiResponseId(trimmed));
    }
    
    /// <summary>
    /// Creates an AiResponseId without validation for internal use.
    /// Use only when the value is guaranteed to be valid.
    /// </summary>
    internal static AiResponseId FromTrustedSource(string value)
    {
        return new AiResponseId(value);
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
            errors.Add(Error.Validation("AI response ID is required", "CHAT.AI.RESPONSE_ID.EMPTY"));
        }
        else if (Value.Length < MinLength)
            errors.Add(Error.Validation($"AI response ID must be at least {MinLength} characters", "CHAT.AI.RESPONSE_ID.TOO_SHORT"));
        else if (Value.Length > MaxLength)
        {
            errors.Add(Error.Validation($"AI response ID cannot exceed {MaxLength} characters", "CHAT.AI.RESPONSE_ID.TOO_LONG"));
        }
        
        return errors.Count == 0 
            ? Validation.Valid(Unit.Value)
            : Validation.Invalid<Unit>(errors);
    }
    
    public override string ToString() => Value;
}