using Axon.Modules.Chat.Application.DTOs.Configurations;

namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Builds provider-specific MCP tool definitions from generic MCP server configuration
/// </summary>
/// <typeparam name="TToolDefinition">The provider-specific tool definition type</typeparam>
public interface IMcpToolBuilder<out TToolDefinition>
{
    /// <summary>
    /// Builds provider-specific tool definitions from MCP server configurations
    /// </summary>
    /// <param name="configs">MCP server configurations</param>
    /// <returns>Array of provider-specific tool definitions, or null if no configs provided</returns>
    TToolDefinition[]? BuildTools(IReadOnlyCollection<McpServerConfig>? configs);
}
