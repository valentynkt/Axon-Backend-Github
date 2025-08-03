namespace Axon.Modules.Chat.Infrastructure.Ai.Models;

/// <summary>
/// OpenAI Responses API response model
/// </summary>
public sealed record ResponsesApiResponse(
    string? Id,
    OutputItem[]? Output);

/// <summary>
/// Output item in the OpenAI Responses API response
/// </summary>
public sealed record OutputItem(
    string Type,
    object? Content);

/// <summary>
/// Message content within an output item
/// </summary>
public sealed record MessageContent(
    string Type,
    string Text);

/// <summary>
/// MCP tool list content
/// </summary>
public sealed record McpToolListContent(
    string ServerLabel,
    McpToolDefinition[] Tools);

/// <summary>
/// MCP tool definition from API response
/// </summary>
public sealed record McpToolDefinition(
    string Name,
    string? Description,
    object? InputSchema);

/// <summary>
/// MCP call item in the response (legacy, keeping for compatibility)
/// </summary>
public sealed record McpCallItem(
    string? ToolName,
    object? Arguments,
    object? Output,
    string? Error);