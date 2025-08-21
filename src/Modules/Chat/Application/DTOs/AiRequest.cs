using Axon.BuildingBlocks.Core.Primitives.ValueObjects;

namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// AI processing request with multiple MCP server configurations
/// </summary>
public sealed record AiRequest(
    MessageContent Message,
    IReadOnlyCollection<McpServerConfig>? McpConfigs = null,
    string? PreviousResponseId = null);