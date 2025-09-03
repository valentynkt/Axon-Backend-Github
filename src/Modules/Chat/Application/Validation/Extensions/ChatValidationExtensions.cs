// /Modules/Chat/Application/Validation/Extensions/ChatValidationExtensions.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;          // MessageContent (Vogen)
using Axon.Modules.Chat.Domain.ValueObjects;                     // ConversationTitle (Vogen bridge with CFE)
using BuildingBlocks.Application.Validation.Constants;           // ValidationErrorCodes
using FluentValidation;
using FluentValidation.Results;

namespace Axon.Modules.Chat.Application.Validation.Extensions;

/// <summary>
/// Chat-specific validation extensions that delegate to domain VOs wherever possible.
/// Keep these thin: they should not duplicate domain rules.
/// </summary>
public static class ChatValidationExtensions
{
    /// <summary>
    /// Validates message content using the Vogen VO as the single source of truth.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidMessageContent<T>(
        this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must((_, value) =>
            {
                // Avoid discard to support stricter language versions.
                return MessageContent.TryParse(value, provider: null, out var _parsed);
            })
            .WithErrorCode(ValidationErrorCodes.ContentInvalid)
            .WithMessage("Message content is invalid.");

    /// <summary>
    /// Validates conversation title by delegating to the domain VO's Create(..) bridge (CFE Result).
    /// Preserves domain-crafted error message and code.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidConversationTitle<T>(
        this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Custom((title, context) =>
            {
                var result = ConversationTitle.Create(title);
                if (result.IsFailure)
                {
                    context.AddFailure(new ValidationFailure(context.PropertyPath, result.Error.Message)
                    {
                        ErrorCode = result.Error.Code
                    });
                }
            });

    /// <summary>
    /// Minimal guard for AI response id (opaque provider string).
    /// We only require a non-empty, non-whitespace value.
    /// Further semantics are enforced in the domain aggregate.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidAiResponseId<T>(
        this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithErrorCode(ValidationErrorCodes.StringEmptyOrWhitespace)
            .WithMessage("AI response ID cannot be empty.");
}
