using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.DTOs.Configurations;

namespace Axon.Modules.Chat.Application.DTOs.Requests;

/// <summary>
/// AI processing request with multiple MCP server configurations
/// </summary>
public sealed record AiRequest(
    MessageContent Message,
    IReadOnlyCollection<McpServerConfig>? McpConfigs = null,
    string? PreviousResponseId = null);