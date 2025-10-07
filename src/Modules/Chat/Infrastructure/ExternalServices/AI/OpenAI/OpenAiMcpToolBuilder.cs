using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI;

/// <summary>
/// Builds OpenAI-specific MCP tool definitions from generic MCP server configuration
/// </summary>
public sealed class OpenAiMcpToolBuilder : IMcpToolBuilder<McpToolDefinition>
{
    public McpToolDefinition[]? BuildTools(IReadOnlyCollection<McpServerConfig>? configs)
    {
        if (configs is null || configs.Count == 0)
            return null;

        var tools = new McpToolDefinition[configs.Count];
        var index = 0;

        foreach (var cfg in configs)
        {
            tools[index++] = new McpToolDefinition
            {
                Type = "mcp",
                ServerUrl = cfg.ServerUrl,
                ServerLabel = string.IsNullOrWhiteSpace(cfg.ServerLabel) ? "MCP Server" : cfg.ServerLabel,
                RequireApproval = cfg.RequireApproval ? "always" : "never",
                Headers = cfg.Headers is { Count: > 0 } ? cfg.Headers : null,
                AllowedTools = cfg.AllowedTools is { Length: > 0 } ? cfg.AllowedTools : null
            };
        }

        return tools;
    }
}
