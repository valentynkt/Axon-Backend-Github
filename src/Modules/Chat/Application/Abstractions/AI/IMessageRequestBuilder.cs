using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Abstractions.AI;

/// <summary>
/// Service for building AI requests with MCP configuration integration
/// </summary>
public interface IMessageRequestBuilder
{
    /// <summary>
    /// Builds an AI request with integrated MCP configuration
    /// </summary>
    /// <param name="message">Message content to process</param>
    /// <param name="previousResponseId">Previous AI response ID for context continuity</param>
    /// <returns>Result containing AI request and MCP server count</returns>
    Result<(AiRequest Request, int McpServerCount)> BuildAiRequest(string message, string? previousResponseId = null);
}