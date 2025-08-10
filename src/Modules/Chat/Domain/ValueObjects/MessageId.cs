using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Type-safe identifier for Chat Messages.
/// Ensures non-empty GUIDs and provides factory methods for creation and parsing.
/// </summary>
public sealed record MessageId : GuidStrongId
{
    private MessageId(Guid value) : base(value) { }

    /// <summary>
    /// Creates a new MessageId with a newly generated GUID.
    /// </summary>
    public static MessageId New() => New(static guid => new MessageId(guid));

    /// <summary>
    /// Creates a MessageId from an existing GUID.
    /// Throws ArgumentException if the GUID is empty.
    /// </summary>
    public static MessageId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("MessageId cannot be empty GUID.", nameof(value));
        
        return new MessageId(value);
    }

    /// <summary>
    /// Attempts to create a MessageId from a string representation.
    /// Returns a Result with appropriate error codes for invalid input.
    /// </summary>
    public static Result<MessageId> FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<MessageId>.Failure(Error.Validation(
                "MessageId cannot be empty.", 
                "CHAT.ID.EMPTY"));

        if (!Guid.TryParse(value, out var parsed))
            return Result<MessageId>.Failure(Error.Validation(
                "MessageId has invalid format.", 
                "CHAT.ID.INVALID_FORMAT"));

        if (parsed == Guid.Empty)
            return Result<MessageId>.Failure(Error.Validation(
                "MessageId cannot be empty GUID.", 
                "CHAT.ID.EMPTY"));

        return Result<MessageId>.Success(new MessageId(parsed));
    }
}