using BuildingBlocks.Core.Domain.Rules;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Rules;

/// <summary>
/// Ensures consistency between message role and AI response ID presence.
/// - Assistant messages MUST have an AI response ID
/// - User messages MUST NOT have an AI response ID
/// This is a critical invariant for maintaining conversation integrity.
/// </summary>
internal sealed class MessageAiResponseIdConsistencyRule : BusinessRule
{
    private readonly MessageRole _role;
    private readonly AiResponseId? _aiResponseId;

    public MessageAiResponseIdConsistencyRule(
        MessageRole role,
        AiResponseId? aiResponseId)
        : base(
            message: GetErrorMessage(role, aiResponseId),
            code: GetErrorCode(role, aiResponseId))
    {
        _role = role ?? throw new ArgumentNullException(nameof(role));
        _aiResponseId = aiResponseId;
    }

    public override bool IsBroken()
    {
        // Assistant messages MUST have an AI response ID
        if (_role.IsAssistant && _aiResponseId is null)
            return true;

        // User messages MUST NOT have an AI response ID
        if (_role.IsUser && _aiResponseId is not null)
            return true;

        return false;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());

    private static string GetErrorMessage(MessageRole role, AiResponseId? aiResponseId)
    {
        if (role?.IsAssistant == true && aiResponseId is null)
            return "Assistant messages must have an AI response ID";

        if (role?.IsUser == true && aiResponseId is not null)
            return "User messages cannot have an AI response ID";

        return "Invalid message role and AI response ID combination";
    }

    private static string GetErrorCode(MessageRole role, AiResponseId? aiResponseId)
    {
        if (role?.IsAssistant == true && aiResponseId is null)
            return "CHAT.MESSAGE.ASSISTANT_MISSING_AI_RESPONSE_ID";

        if (role?.IsUser == true && aiResponseId is not null)
            return "CHAT.MESSAGE.USER_HAS_AI_RESPONSE_ID";

        return "CHAT.MESSAGE.INVALID_ROLE_AI_RESPONSE_COMBINATION";
    }
}