using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for chat conversations
/// </summary>
public readonly record struct ConversationId
{
    public Guid Value { get; }

    private ConversationId(Guid value) => Value = value;

    /// <summary>
    /// Creates a new unique ConversationId
    /// </summary>
    /// <returns>A new ConversationId with a unique GUID</returns>
    public static ConversationId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a ConversationId from an existing GUID value with validation
    /// </summary>
    /// <param name="value">The GUID value to use</param>
    /// <returns>Result containing ConversationId or validation error</returns>
    public static Result<ConversationId> Create(Guid value) =>
        value == Guid.Empty 
            ? Error.Validation("ConversationId cannot be empty")
            : new ConversationId(value);

    /// <summary>
    /// Creates a ConversationId from a string representation
    /// </summary>
    /// <param name="value">The string representation of a GUID</param>
    /// <returns>Result containing ConversationId or validation error</returns>
    public static Result<ConversationId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("ConversationId string cannot be null or empty");
            
        return Guid.TryParse(value, out var guid)
            ? Create(guid)
            : Error.Validation("ConversationId must be a valid GUID format");
    }

    public override string ToString() => Value.ToString();
    
    public static implicit operator Guid(ConversationId conversationId) => conversationId.Value;
}