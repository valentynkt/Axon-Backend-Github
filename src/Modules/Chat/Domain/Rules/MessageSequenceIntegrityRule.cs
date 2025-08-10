using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.Entities;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Validates that messages have consecutive sequences starting from 1.
/// This is an invariant check to ensure sequence integrity.
/// </summary>
internal sealed class MessageSequenceIntegrityRule : BusinessRule
{
    private readonly IReadOnlyList<Message> _messages;

    public MessageSequenceIntegrityRule(IReadOnlyList<Message> messages)
        : base(
            message: "Message sequence must be contiguous starting at 1.",
            code: "CHAT_INVARIANT_SEQUENCE_VIOLATION")
    {
        _messages = messages ?? new List<Message>();
    }

    public override bool IsBroken()
    {
        if (_messages.Count == 0)
            return false;

        // Check that each message has the correct sequence number (1-based)
        for (int i = 0; i < _messages.Count; i++)
        {
            if (_messages[i].Sequence != i + 1)
                return true;
        }

        return false;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}