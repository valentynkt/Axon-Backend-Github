using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Business rule that enforces strict alternation: no consecutive messages from the same role.
/// Also ensures that users must start conversations (no assistant-first messages).
/// Implements requirements: strict alternation and user-must-start policy.
/// </summary>
internal sealed class MessageTurnTakingRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;
    private readonly MessageRole _newRole;

    public MessageTurnTakingRule(IReadOnlyList<Message> messages, MessageRole newRole)
        : base(
            message: "Messages must alternate between user and assistant.",
            code: "CHAT.MESSAGE.TURN.VIOLATION")
    {
        _messages = messages ?? new List<Message>();
        _newRole = newRole;
    }

    public override bool IsBroken()
    {
        // Empty conversation - only user can be first (no assistant-first messages)
        if (_messages.Count == 0)
            return _newRole.IsAssistant;

        // Block consecutive messages from the same role (strict alternation)
        var lastMessage = _messages[^1];
        return lastMessage.Role.Value == _newRole.Value;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}