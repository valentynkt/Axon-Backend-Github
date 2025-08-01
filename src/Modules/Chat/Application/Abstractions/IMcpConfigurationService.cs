using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for managing MCP server configuration loading and validation
/// </summary>
public interface IMcpConfigurationService
{
    /// <summary>
    /// Loads enabled MCP server configurations
    /// </summary>
    /// <returns>Result containing enabled MCP server configurations</returns>
    Result<IReadOnlyCollection<McpServerConfig>> LoadEnabledConfigurations();

    /// <summary>
    /// Validates MCP server configuration
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateConfiguration(McpServerConfig config);
}