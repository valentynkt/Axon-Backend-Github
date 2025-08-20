// /Axon/Modules/Chat/Domain/ValueObjects/MessageRole.cs
#nullable enable
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Vogen;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Message role as a validated value object (Vogen).
/// Allowed values (case-insensitive input, canonical storage):
/// - "user"
/// - "assistant"
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct MessageRole
{
    public const string UserValue      = "user";
    public const string AssistantValue = "assistant";

    /// <summary>Normalize inputs before validation (trim + lower-invariant).</summary>
    private static string NormalizeInput(string input) => input.Trim().ToLowerInvariant();

    /// <summary>Validate normalized input.</summary>
    private static Validation Validate(string input)
        => input is UserValue or AssistantValue
            ? Validation.Ok
            : Validation.Invalid("Invalid message role (allowed: user, assistant).");

    /// <summary>Convenience: is this "user"?</summary>
    public bool IsUser => Value == UserValue;

    /// <summary>Convenience: is this "assistant"?</summary>
    public bool IsAssistant => Value == AssistantValue;

    /// <summary>Predefined canonical roles.</summary>
    public static MessageRole User      => From(UserValue);
    public static MessageRole Assistant => From(AssistantValue);

    public override string ToString() => Value;

    /// <summary>
    /// Non-throwing factory bridging Vogen to CFE <c>Result</c>.
    /// Accepts any casing, trims whitespace, stores canonical lowercase.
    /// </summary>
    public static Result<MessageRole, Error> FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<MessageRole, Error>(
                Error.Validation("Invalid message role (allowed: user, assistant).", "CHAT.ROLE.INVALID"));

        try
        {
            var vo = From(value); // Vogen will normalize + validate
            return Result.Success<MessageRole, Error>(vo);
        }
        catch (ValidationException vex)
        {
            return Result.Failure<MessageRole, Error>(
                Error.Validation(vex.Message, "CHAT.ROLE.INVALID"));
        }
    }
}
