using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Configurations;
 

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.MCP;

/// <summary>
/// Service for resolving MCP server configurations from centralized settings
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class McpServerResolver : IMcpServerResolver
{
    private readonly McpServersOptions _mcpServersOptions;
    private readonly ILogger<McpServerResolver> _logger;

    public McpServerResolver(
        IOptions<McpServersOptions> mcpServersOptions,
        ILogger<McpServerResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(mcpServersOptions);
        _mcpServersOptions = mcpServersOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<McpServerConfig[]> ResolveServersAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Async for future expansion
        
        var enabledServers = new List<McpServerConfig>();

        foreach (var (configKey, serverConfig) in _mcpServersOptions.Servers)
        {
            // Skip disabled servers
            if (!ShouldIncludeServer(serverConfig))
            {
                LogAndSkipDisabledServer(configKey);
                continue;
            }

            // Convert to domain model
            var mcpConfig = ToMcpServerConfig(configKey, serverConfig);
            enabledServers.Add(mcpConfig);

            _logger.LogDebug(
                "Loaded enabled MCP server '{ConfigKey}' at URL '{ServerUrl}' with {ToolCount} allowed tools",
                configKey,
                serverConfig.ServerUrl,
                serverConfig.AllowedTools?.Length ?? 0);
        }

        _logger.LogInformation("Loaded {EnabledCount} enabled MCP servers from configuration", enabledServers.Count);
        
        return enabledServers.ToArray();
    }
    
    /// <inheritdoc />
    public async Task<McpServerConfig?> GetServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Async for future expansion
        
        if (_mcpServersOptions.Servers.TryGetValue(serverId, out var serverConfig) && serverConfig.Enabled)
        {
            return ToMcpServerConfig(serverId, serverConfig);
        }
        
        return null;
    }

    private static bool ShouldIncludeServer(McpServerOptions serverConfig) => serverConfig.Enabled;

    private static McpServerConfig ToMcpServerConfig(string configKey, McpServerOptions serverConfig)
    {
        return new McpServerConfig(
            ServerUrl: serverConfig.ServerUrl,
            ServerLabel: serverConfig.ServerLabel ?? configKey,
            Headers: serverConfig.Headers?.Count > 0 ? serverConfig.Headers : null,
            AllowedTools: serverConfig.AllowedTools?.Length > 0 ? serverConfig.AllowedTools : null,
            RequireApproval: serverConfig.RequireApproval,
            TimeoutSeconds: serverConfig.TimeoutSeconds);
    }

    private void LogAndSkipDisabledServer(string configKey)
    {
        _logger.LogDebug("Skipping disabled MCP server '{ConfigKey}'", configKey);
    }
}