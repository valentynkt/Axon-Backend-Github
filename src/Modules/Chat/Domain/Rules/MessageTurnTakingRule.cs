using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Enforces turn-taking rules for messages.
/// Enforces APP-01 rule: User cannot send two messages in a row. Either role can be first.
/// </summary>
internal sealed class MessageTurnTakingRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;
    private readonly MessageRole _newRole;

    public MessageTurnTakingRule(IReadOnlyList<Message> messages, MessageRole newRole)
        : base(
            message: "User cannot send two messages in a row.",
            code: "CHAT.MESSAGE.USER.TURN.VIOLATION")
    {
        _messages = messages ?? new List<Message>();
        _newRole = newRole ?? throw new ArgumentNullException(nameof(newRole));
    }

    public override bool IsBroken()
    {
        // Empty conversation - any role is allowed (assistant can be first)
        if (_messages.Count == 0)
            return false;

        // Block consecutive user messages (APP-01).
        var lastMessage = _messages[^1];
        return lastMessage.Role.IsUser && _newRole.IsUser;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}