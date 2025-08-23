using System;
using System.Threading;
using System.Threading.Tasks;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.DTOs.Configurations;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service for resolving MCP (Model Context Protocol) servers for chat operations.
/// </summary>
public sealed class McpServerResolutionService : IMcpServerResolutionService
{
    private readonly IMcpServerResolver _mcpResolver;
    private readonly ILogger<McpServerResolutionService> _logger;

    public McpServerResolutionService(
        IMcpServerResolver mcpResolver,
        ILogger<McpServerResolutionService> logger)
    {
        _mcpResolver = mcpResolver ?? throw new ArgumentNullException(nameof(mcpResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<McpServerConfig[]?> ResolveServersAsync(ConversationId conversationId, CancellationToken cancellationToken)
    {
        try
        {
            var resolved = await _mcpResolver.ResolveServersAsync(cancellationToken);
            if (resolved is { Length: > 0 })
            {
                _logger.LogDebug(
                    "Resolved {ServerCount} MCP servers for conversation {ConversationId}",
                    resolved.Length, conversationId.Value);
                return resolved;
            }

            _logger.LogDebug(
                "No MCP servers resolved for conversation {ConversationId}",
                conversationId.Value);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to resolve MCP servers for conversation {ConversationId}. Continuing without MCP.",
                conversationId.Value);
            return null;
        }
    }
}