using System.Threading;
using System.Threading.Tasks;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for resolving MCP (Model Context Protocol) servers for chat operations.
/// </summary>
public interface IMcpServerResolutionService
{
    /// <summary>
    /// Resolves available MCP servers for a conversation with best-effort error handling.
    /// </summary>
    /// <param name="conversationId">The conversation ID for logging context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of MCP server configurations, or null if resolution fails</returns>
    Task<McpServerConfig[]?> ResolveServersAsync(ConversationId conversationId, CancellationToken cancellationToken);
}