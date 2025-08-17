using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Abstractions.AI;

/// <summary>
/// Service for resolving and managing MCP server configurations
/// </summary>
public interface IMcpServerResolver
{
    /// <summary>
    /// Gets all enabled MCP server configurations
    /// </summary>
    /// <returns>Result containing list of enabled MCP server configurations</returns>
    Result<IReadOnlyList<McpServerConfig>> GetEnabledServerConfigurations();
}