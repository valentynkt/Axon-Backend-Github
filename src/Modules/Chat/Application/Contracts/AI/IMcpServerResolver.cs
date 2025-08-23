using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.DTOs.Configurations;

namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Service for resolving MCP server configurations
/// </summary>
public interface IMcpServerResolver
{
    /// <summary>
    /// Resolve MCP server configurations
    /// </summary>
    Task<McpServerConfig[]> ResolveServersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific MCP server configuration
    /// </summary>
    Task<McpServerConfig?> GetServerAsync(string serverId, CancellationToken cancellationToken = default);
}