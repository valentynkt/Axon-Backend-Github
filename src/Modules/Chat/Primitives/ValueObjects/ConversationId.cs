using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Primitives.ValueObjects;

/// <summary>
/// Type-safe identifier for Chat Conversations.
/// Ensures non-empty GUIDs and provides factory methods for creation and parsing.
/// </summary>
public sealed record ConversationId : GuidStrongId
{
    private ConversationId(Guid value) : base(value) { }

    /// <summary>
    /// Creates a new ConversationId with a newly generated GUID.
    /// </summary>
    public static ConversationId New() => New(static guid => new ConversationId(guid));

    /// <summary>
    /// Creates a ConversationId from an existing GUID.
    /// Throws ArgumentException if the GUID is empty.
    /// </summary>
    public static ConversationId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ConversationId cannot be empty GUID.", nameof(value));
        
        return new ConversationId(value);
    }

    /// <summary>
    /// Attempts to create a ConversationId from a string representation.
    /// Returns a Result with appropriate error codes for invalid input.
    /// </summary>
    public static Result<ConversationId> FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<ConversationId>.Failure(Error.Validation(
                "ConversationId cannot be empty.", 
                "CHAT.ID.EMPTY"));

        if (!Guid.TryParse(value, out var parsed))
            return Result<ConversationId>.Failure(Error.Validation(
                "ConversationId has invalid format.", 
                "CHAT.ID.INVALID_FORMAT"));

        if (parsed == Guid.Empty)
            return Result<ConversationId>.Failure(Error.Validation(
                "ConversationId cannot be empty GUID.", 
                "CHAT.ID.EMPTY"));

        return Result<ConversationId>.Success(new ConversationId(parsed));
    }
}