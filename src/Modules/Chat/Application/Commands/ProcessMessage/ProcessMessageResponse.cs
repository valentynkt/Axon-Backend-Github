using Axon.Modules.Chat.Domain.Types;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Response from processing a chat message
/// </summary>
public sealed record ProcessMessageResponse(
    string Response,
    Guid? ConversationId,
    ToolExecution[]? ToolExecutions);