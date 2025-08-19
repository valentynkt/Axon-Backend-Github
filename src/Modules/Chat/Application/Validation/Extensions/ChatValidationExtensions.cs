using FluentValidation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Primitives.ValueObjects;

namespace Axon.Modules.Chat.Application.Validation.Extensions;

/// <summary>
/// Chat-specific validation extensions that use domain validation as single source of truth
/// </summary>
public static class ChatValidationExtensions
{
    /// <summary>
    /// Validates message content using domain validation as single source of truth
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidMessageContent<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Custom((content, context) =>
            {
                if (string.IsNullOrEmpty(content))
                {
                    context.AddFailure("Message content cannot be empty.");
                    return;
                }

                var result = MessageContent.Create(content);
                if (result.IsFailure)
                {
                    context.AddFailure(result.Error.Message);
                }
            });
    }

    /// <summary>
    /// Validates conversation title using domain validation as single source of truth
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidConversationTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Custom((title, context) =>
            {
                if (string.IsNullOrEmpty(title))
                {
                    context.AddFailure("Conversation title cannot be empty.");
                    return;
                }

                var result = ConversationTitle.Create(title);
                if (result.IsFailure)
                {
                    context.AddFailure(result.Error.Message);
                }
            });
    }

    /// <summary>
    /// Validates AI response ID using domain validation as single source of truth
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string> MustBeValidAiResponseId<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Custom((responseId, context) =>
            {
                if (string.IsNullOrEmpty(responseId))
                {
                    context.AddFailure("AI response ID cannot be empty.");
                    return;
                }

                var result = AiResponseId.Create(responseId);
                if (result.IsFailure)
                {
                    context.AddFailure(result.Error.Message);
                }
            });
    }
}