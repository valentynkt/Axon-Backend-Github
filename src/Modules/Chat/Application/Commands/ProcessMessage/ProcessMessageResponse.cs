namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Response containing processed message with tool results
/// </summary>
public sealed record ProcessMessageResponse(
    string Response,
    string? ConversationId = null,
    ToolExecutionSummary[]? ToolExecutions = null);

/// <summary>
/// Tool execution summary for API responses
/// </summary>
public sealed record ToolExecutionSummary(
    string ToolName,
    bool Success,
    TimeSpan Duration);