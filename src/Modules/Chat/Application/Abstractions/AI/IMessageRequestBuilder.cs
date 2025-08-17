using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
 
using BuildingBlocks.Core.Functional.Results;
using AiRequest = Axon.Modules.Chat.Application.Commands.ProcessMessage.AiRequest;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for building AI requests with MCP configuration integration
/// </summary>
public interface IMessageRequestBuilder
{
    /// <summary>
    /// Builds an AI request with integrated MCP configuration
    /// </summary>
    /// <param name="command">Process message command</param>
    /// <param name="conversationContext">Optional conversation context for continuity</param>
    /// <returns>Result containing AI request and MCP server count</returns>
    Result<(AiRequest Request, int McpServerCount)> BuildAiRequest(ProcessMessageCommand command, string? conversationContext = null);
}