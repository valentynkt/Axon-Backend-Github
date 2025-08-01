namespace Axon.Modules.Chat.Infrastructure.Ai.Models;

/// <summary>
/// OpenAI Responses API response model
/// </summary>
public sealed record ResponsesApiResponse(
    string? Id,
    string? OutputText,
    McpCallItem[]? McpCalls);

/// <summary>
/// MCP call item in the response
/// </summary>
public sealed record McpCallItem(
    string? ToolName,
    object? Arguments,
    object? Output,
    string? Error);