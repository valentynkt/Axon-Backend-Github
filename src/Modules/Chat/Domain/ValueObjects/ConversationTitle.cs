// /Axon/Modules/Chat/Domain/ValueObjects/ConversationTitle.cs
#nullable enable
using Axon.BuildingBlocks.Core.Constants;
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Conversation title as a validated value object (Vogen).
/// Rules:
/// - Trim leading/trailing whitespace
/// - Must be non-empty after trimming
/// - Max length: <see cref="ConversationTitle.MaxLength"/>
/// Generated converters: System.Text.Json, TypeConverter, EF Core.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct ConversationTitle
{
    private const int MaxLength = ChatPrimitiveConstants.ConversationTitleDefault.MaxLength;

    /// <summary>Normalize inputs before validation.</summary>
    private static string NormalizeInput(string input) => input.Trim();

    /// <summary>Validate normalized input.</summary>
    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("Title cannot be empty.");

        if (input.Length > MaxLength)
            return Validation.Invalid($"Title cannot exceed {MaxLength} characters. Current length: {input.Length}.");

        return Validation.Ok;
    }

    /// <summary>Length convenience.</summary>
    public int Length => Value.Length;

    /// <summary>Simple preview with hard cutoff (no ellipsis).</summary>
    public string Preview(int maxLength = ChatPrimitiveConstants.ConversationDefault.ContentPreviewLength)
        => maxLength <= 0 ? string.Empty : (Value.Length <= maxLength ? Value : Value[..maxLength]);

    public override string ToString() => Value;

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// </summary>
    public static Result<ConversationTitle, Error> Create(string? value)
    {
        if (value is null)
            return Result.Failure<ConversationTitle, Error>(
                Error.Validation("Title is required.", "CHAT.CONVERSATION.TITLE.REQUIRED"));

        try
        {
            // Vogen validates via NormalizeInput + Validate
            var vo = From(value);
            return Result.Success<ConversationTitle, Error>(vo);
        }
        catch (ValueObjectValidationException vex)
        {
            return Result.Failure<ConversationTitle, Error>(
                Error.Validation(vex.Message, "CHAT.CONVERSATION.TITLE.INVALID"));
        }
    }
}
