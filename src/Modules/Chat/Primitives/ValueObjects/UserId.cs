using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Primitives.ValueObjects;

/// <summary>
/// Type-safe identifier for Users in the Chat domain context.
/// Ensures non-empty GUIDs and provides factory methods for creation and parsing.
/// </summary>
public sealed record UserId : GuidStrongId
{
    private UserId(Guid value) : base(value) { }

    /// <summary>
    /// Creates a new UserId with a newly generated GUID.
    /// </summary>
    public static UserId New() => New(static guid => new UserId(guid));

    /// <summary>
    /// Creates a UserId from an existing GUID.
    /// Throws ArgumentException if the GUID is empty.
    /// </summary>
    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty GUID.", nameof(value));
        
        return new UserId(value);
    }

    /// <summary>
    /// Attempts to create a UserId from a string representation.
    /// Returns a Result with appropriate error codes for invalid input.
    /// </summary>
    public static Result<UserId> FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<UserId>.Failure(Error.Validation(
                "UserId cannot be empty.", 
                "CHAT.ID.EMPTY"));

        if (!Guid.TryParse(value, out var parsed))
            return Result<UserId>.Failure(Error.Validation(
                "UserId has invalid format.", 
                "CHAT.ID.INVALID_FORMAT"));

        if (parsed == Guid.Empty)
            return Result<UserId>.Failure(Error.Validation(
                "UserId cannot be empty GUID.", 
                "CHAT.ID.EMPTY"));

        return Result<UserId>.Success(new UserId(parsed));
    }
}