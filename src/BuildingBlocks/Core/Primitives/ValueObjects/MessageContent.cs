using System.Diagnostics;
using Vogen;
using Axon.BuildingBlocks.Core.Constants;

namespace Axon.BuildingBlocks.Core.Primitives.ValueObjects;

/// <summary>
/// Chat message content with compile-time validation and generated converters.
/// Creation: <c>MessageContent.From("...")</c> (throws on invalid)
/// Non-throwing: <c>MessageContent.TryParse("...", out var vo)</c>
/// JSON: STJ converter generated
/// EF Core: value converter generated
/// TypeConverter: generated (useful for binding, config, etc.)
/// </summary>
[DebuggerDisplay("{Value}")]
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct MessageContent
{
    private const int MaxLength = ChatPrimitiveConstants.MessageContent.MaxLength;

    // Vogen will call this before Validate and before storing the value
    private static string NormalizeInput(string input) => input.Trim();

    // Vogen passes the normalized input here
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Message content cannot be empty or whitespace.");

        if (input.Length > MaxLength)
            return Validation.Invalid($"Message content cannot exceed {MaxLength} characters. Current length: {input.Length}.");

        return Validation.Ok;
    }

    /// <summary>Preview (no ellipsis; hard cutoff).</summary>
    public string Preview(int maxLength = ChatPrimitiveConstants.Conversation.ContentPreviewLength)
        => maxLength <= 0 ? string.Empty : (Value.Length <= maxLength ? Value : Value[..maxLength]);

    public override string ToString() => Value;
}