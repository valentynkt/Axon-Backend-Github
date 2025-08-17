using Axon.Modules.Chat.Application.DTOs;
 
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for resolving MCP server configurations from centralized settings
/// </summary>
public interface IMcpServerResolver
{
    /// <summary>
    /// Get all enabled MCP server configurations
    /// </summary>
    /// <returns>Collection of enabled MCP server configurations</returns>
    Result<IReadOnlyCollection<McpServerConfig>> GetEnabledServerConfigurations();
}