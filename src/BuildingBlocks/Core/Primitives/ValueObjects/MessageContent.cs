using Vogen;
using Axon.BuildingBlocks.Core.Constants;

namespace Axon.BuildingBlocks.Core.Primitives.ValueObjects;

/// <summary>
/// Chat message content with compile-time validation and generated converters.
/// <para>Creation: MessageContent.From("...")  // throws on invalid</para>
/// <para>Non-throwing: MessageContent.TryParse("...", out var vo)</para>
/// <para>JSON: System.Text.Json converter generated</para>
/// <para>EF Core: value converter generated (see OnModelCreating note below)</para>
/// <para>TypeConverter: generated (useful for binding, config, etc.)</para>
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct MessageContent
{
    private const int MaxLength = ChatPrimitiveConstants.MessageContent.MaxLength;

    // Normalize before validation/persistence (keeps stored value trimmed)
    private static string NormalizeInput(string input) => input.Trim();

    // Business invariants - validates the normalized input
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Message content cannot be empty or whitespace.");

        if (input.Length > MaxLength)
            return Validation.Invalid($"Message content cannot exceed {MaxLength} characters. Current length: {input.Length}.");

        return Validation.Ok;
    }

    /// <summary>
    /// Gets a preview of the message content, truncated to the specified length.
    /// </summary>
    /// <param name="maxLength">Maximum length for the preview. Defaults to the standard preview length.</param>
    /// <returns>The message content truncated to the specified length, or the full content if shorter.</returns>
    public string Preview(int maxLength = ChatPrimitiveConstants.Conversation.ContentPreviewLength)
        => maxLength <= 0 ? string.Empty : (Value.Length <= maxLength ? Value : Value[..maxLength]);

    /// <summary>
    /// Returns the string representation of the message content.
    /// </summary>
    public override string ToString() => Value;
}