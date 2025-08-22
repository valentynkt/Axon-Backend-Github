// /BuildingBlocks/Core/Primitives/ValueObjects/MessageContent.cs
#nullable enable
using System.Diagnostics;
using Vogen;
using Axon.BuildingBlocks.Core.Constants;
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.BuildingBlocks.Core.Primitives.ValueObjects;

/// <summary>
/// Chat message content with compile-time validation and generated converters.
/// Creation: <c>MessageContent.From("...")</c> (throws on invalid)
/// Non-throwing: <c>MessageContent.TryParse("...", provider, out var vo)</c>
/// JSON: STJ converter generated
/// EF Core: value converter generated
/// TypeConverter: generated (useful for binding, config, etc.)
/// </summary>
[DebuggerDisplay("{Value}")]
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct MessageContent
{
    private const int MaxLength = ChatPrimitiveConstants.MessageContentDefault.MaxLength;

    // Vogen will call this before Validate and before storing the value
    private static string NormalizeInput(string input) => input.Trim();

    // Vogen passes the normalized input here
    private static Vogen.Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Vogen.Validation.Invalid("Message content cannot be empty or whitespace.");

        if (input.Length > MaxLength)
            return Vogen.Validation.Invalid($"Message content cannot exceed {MaxLength} characters. Current length: {input.Length}.");

        return Vogen.Validation.Ok;
    }

    /// <summary>Preview (no ellipsis; hard cutoff).</summary>
    public string Preview(int maxLength = ChatPrimitiveConstants.ConversationDefault.ContentPreviewLength)
        => maxLength <= 0 ? string.Empty : (Value.Length <= maxLength ? Value : Value[..maxLength]);

    public override string ToString() => Value;

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Preferred in application layer to avoid exception-based control flow.
    /// </summary>
    public static Result<MessageContent, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<MessageContent, Error>(
                Error.Validation("Message content cannot be empty or whitespace.", "CHAT.MESSAGE.EMPTY"));
        }

        // Use generated TryParse; provider null is fine
        return TryParse(value, provider: null, out var vo)
            ? Result.Success<MessageContent, Error>(vo)
            : Result.Failure<MessageContent, Error>(
                Error.Validation("Message content is invalid.", "CHAT.MESSAGE.INVALID"));
    }
}
