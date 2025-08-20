using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Rule to validate that AI Response ID is unique within the conversation.
/// </summary>
internal sealed class AiResponseIdMustBeUniqueRule : BusinessRule
{
    private readonly AiResponseId _aiResponseId;
    private readonly IReadOnlyList<Message> _existingMessages;

    public AiResponseIdMustBeUniqueRule(AiResponseId aiResponseId, IReadOnlyList<Message> existingMessages)
        : base(
            message: "AI Response ID must be unique within the conversation to prevent duplicate responses.",
            code: "CHAT.AI.RESPONSE.ID.DUPLICATE")
    {
        _aiResponseId = aiResponseId;
        _existingMessages = existingMessages ?? throw new ArgumentNullException(nameof(existingMessages));
    }

    public override bool IsBroken()
    {
        // Check if any existing message already uses this AI Response ID
        return _existingMessages
            .Where(m => m.Role.IsAssistant)
            .Any(m => m.AiResponseId?.Equals(_aiResponseId) == true);
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}