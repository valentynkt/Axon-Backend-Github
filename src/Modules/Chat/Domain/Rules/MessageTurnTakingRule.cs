using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Enforces turn-taking rules for messages.
/// MVP: No consecutive assistant messages allowed. Assistant CAN be first message.
/// </summary>
internal sealed class MessageTurnTakingRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;
    private readonly MessageRole _newRole;

    public MessageTurnTakingRule(IReadOnlyList<Message> messages, MessageRole newRole)
        : base(
            message: "Assistant cannot send two messages in a row.",
            code: "CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION")
    {
        _messages = messages ?? new List<Message>();
        _newRole = newRole ?? throw new ArgumentNullException(nameof(newRole));
    }

    public override bool IsBroken()
    {
        // Empty conversation - any role is allowed (assistant can be first)
        if (_messages.Count == 0)
            return false;

        // Check if trying to add consecutive assistant messages
        var lastMessage = _messages[^1];
        return lastMessage.Role.IsAssistant && _newRole.IsAssistant;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}