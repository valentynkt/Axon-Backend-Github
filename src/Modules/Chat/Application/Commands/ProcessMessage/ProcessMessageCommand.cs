namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Internal command for processing messages through AI
/// Used by AppendUserMessageHandler to structure AI requests
/// </summary>
internal sealed record ProcessMessageCommand(
    string Message,
    string? PreviousResponseId = null
);

/// <summary>
/// Lightweight AI request structure for internal use
/// Maps to Application DTOs/AiRequest
/// </summary>
internal sealed record AiRequest(
    string Message,
    IReadOnlyCollection<object>? McpConfigs = null,
    string? PreviousResponseId = null
);