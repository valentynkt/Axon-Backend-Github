using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for chat messages
/// </summary>
public readonly record struct MessageId
{
    public Guid Value { get; }

    private MessageId(Guid value) => Value = value;

    /// <summary>
    /// Creates a new unique MessageId
    /// </summary>
    /// <returns>A new MessageId with a unique GUID</returns>
    public static MessageId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a MessageId from an existing GUID value with validation
    /// </summary>
    /// <param name="value">The GUID value to use</param>
    /// <returns>Result containing MessageId or validation error</returns>
    public static Result<MessageId> Create(Guid value) =>
        value == Guid.Empty 
            ? Error.Validation("MessageId cannot be empty")
            : new MessageId(value);

    /// <summary>
    /// Creates a MessageId from a string representation
    /// </summary>
    /// <param name="value">The string representation of a GUID</param>
    /// <returns>Result containing MessageId or validation error</returns>
    public static Result<MessageId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Error.Validation("MessageId string cannot be null or empty");
            
        return Guid.TryParse(value, out var guid)
            ? Create(guid)
            : Error.Validation("MessageId must be a valid GUID format");
    }

    public override string ToString() => Value.ToString();
    
    public static implicit operator Guid(MessageId messageId) => messageId.Value;
}