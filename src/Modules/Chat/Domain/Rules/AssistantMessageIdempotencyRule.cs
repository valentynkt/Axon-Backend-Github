using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Ensures idempotency of assistant messages based on AI response ID.
/// Prevents duplicate assistant messages from the same AI response.
/// This is critical for retry scenarios and maintaining conversation integrity.
/// </summary>
internal sealed class AssistantMessageIdempotencyRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;
    private readonly AiResponseId _aiResponseId;

    public AssistantMessageIdempotencyRule(
        IReadOnlyList<Message> messages,
        AiResponseId aiResponseId)
        : base(
            message: "An assistant message with this AI response ID already exists in the conversation",
            code: "CHAT.MESSAGE.AI_RESPONSE_DUPLICATE")
    {
        _messages = messages ?? new List<Message>();
        _aiResponseId = aiResponseId ?? throw new ArgumentNullException(nameof(aiResponseId));
    }

    public override bool IsBroken()
    {
        // Check if any existing message already has this AI response ID
        return _messages.Any(m => m.AiResponseId?.Equals(_aiResponseId) == true);
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}