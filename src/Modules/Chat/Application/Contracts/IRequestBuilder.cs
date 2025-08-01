using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Contract for AI request building - to be implemented by srp-decomposition-specialist
/// </summary>
public interface IRequestBuilder
{
    /// <summary>
    /// Build AI request from command
    /// </summary>
    /// <param name="command">Message processing command</param>
    /// <returns>Result containing AI request and MCP server count</returns>
    Result<(AiRequest Request, int McpServerCount)> BuildAiRequest(ProcessMessageCommand command);
}