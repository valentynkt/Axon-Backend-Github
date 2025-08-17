using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
 
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service responsible for managing MCP server configuration loading and validation
/// </summary>
public sealed class McpConfigurationService : IMcpConfigurationService
{
    private readonly IMcpServerResolver _mcpServerResolver;
    private readonly ILogger<McpConfigurationService> _logger;

    // LoggerMessage delegates for improved performance
    private static readonly Action<ILogger, int, Exception?> ConfigurationsLoadedSuccessfully = 
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(1, nameof(ConfigurationsLoadedSuccessfully)),
            "Successfully loaded {ConfigCount} MCP server configurations");

    private static readonly Action<ILogger, Error, Exception?> ConfigurationsLoadFailed = 
        LoggerMessage.Define<Error>(
            LogLevel.Error,
            new EventId(2, nameof(ConfigurationsLoadFailed)),
            "Failed to load MCP server configurations: {Error}");

    public McpConfigurationService(
        IMcpServerResolver mcpServerResolver,
        ILogger<McpConfigurationService> logger)
    {
        _mcpServerResolver = mcpServerResolver;
        _logger = logger;
    }

    /// <summary>
    /// Loads enabled MCP server configurations
    /// </summary>
    /// <returns>Result containing enabled MCP server configurations</returns>
    public Result<IReadOnlyCollection<McpServerConfig>> LoadEnabledConfigurations()
    {
        var result = _mcpServerResolver.GetEnabledServerConfigurations();
        
        if (result.IsSuccess)
        {
            ConfigurationsLoadedSuccessfully(_logger, result.Value.Count, null);
        }
        else
        {
            ConfigurationsLoadFailed(_logger, result.Error, null);
        }

        return result;
    }

    /// <summary>
    /// Validates MCP server configuration
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool ValidateConfiguration(McpServerConfig config)
    {
        if (config is null)
            return false;

        if (string.IsNullOrWhiteSpace(config.ServerUrl))
            return false;

        if (string.IsNullOrWhiteSpace(config.ServerLabel))
            return false;

        return Uri.TryCreate(config.ServerUrl, UriKind.Absolute, out _);
    }
}