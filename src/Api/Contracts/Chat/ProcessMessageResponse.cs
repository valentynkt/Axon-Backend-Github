namespace Axon.Api.Contracts.Chat;

/// <summary>
/// HTTP response for processed message
/// </summary>
public sealed record ProcessMessageResponse(
    string Response,
    string ConversationId,
    ToolExecutionResponse[]? ToolExecutions = null);

/// <summary>
/// Tool execution information in API response
/// </summary>
public sealed record ToolExecutionResponse(
    string ToolName,
    bool Success,
    int DurationMs);