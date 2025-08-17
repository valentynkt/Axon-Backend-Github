namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Validator for AppendUserMessageCommand
/// </summary>
public sealed class AppendUserMessageValidator : AbstractValidator<AppendUserMessageCommand>
{
    private const int MaxContentLength = 100_000;
    private const int MaxIdempotencyKeyLength = 200;

    public AppendUserMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithErrorCode("CHAT.CONVERSATION.ID.EMPTY")
            .WithMessage("Conversation ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithErrorCode("CHAT.MESSAGE.CONTENT.EMPTY")
            .WithMessage("Message content is required.")
            .Must(c => c?.Trim().Length <= MaxContentLength)
            .WithErrorCode("CHAT.MESSAGE.CONTENT.TOO_LONG")
            .WithMessage($"Message content cannot exceed {MaxContentLength} characters.");

        When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey), () =>
        {
            RuleFor(x => x.IdempotencyKey)
                .Must(key => key!.Trim().Length <= MaxIdempotencyKeyLength)
                .WithErrorCode("CHAT.IDEMPOTENCY.KEY.TOO_LONG")
                .WithMessage($"Idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.");
        });
    }
}