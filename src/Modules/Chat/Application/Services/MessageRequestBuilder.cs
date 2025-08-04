using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service responsible for building AI requests with MCP configuration integration
/// </summary>
public sealed class MessageRequestBuilder : IMessageRequestBuilder
{
    private readonly IMcpServerResolver _mcpServerResolver;
    private readonly ILogger<MessageRequestBuilder> _logger;

    // LoggerMessage delegates for improved performance
    private static readonly Action<ILogger, Error, Exception?> McpConfigurationLoadFailed = 
        LoggerMessage.Define<Error>(
            LogLevel.Error,
            new EventId(1, nameof(McpConfigurationLoadFailed)),
            "Failed to load MCP server configurations: {Error}");

    private static readonly Action<ILogger, int, Exception?> UsingEnabledMcpServers = 
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(2, nameof(UsingEnabledMcpServers)),
            "Using {McpServerCount} enabled MCP servers");

    public MessageRequestBuilder(
        IMcpServerResolver mcpServerResolver,
        ILogger<MessageRequestBuilder> logger)
    {
        _mcpServerResolver = mcpServerResolver;
        _logger = logger;
    }

    /// <summary>
    /// Builds an AI request with integrated MCP configuration
    /// </summary>
    /// <param name="command">Process message command</param>
    /// <param name="conversationContext">Optional conversation context for continuity</param>
    /// <returns>Result containing AI request and MCP server count</returns>
    public Result<(AiRequest Request, int McpServerCount)> BuildAiRequest(ProcessMessageCommand command, string? conversationContext = null)
    {
        if (command is null)
            return Error.Validation("ProcessMessageCommand cannot be null");

        // Get all enabled MCP servers from configuration
        var mcpServersResult = _mcpServerResolver.GetEnabledServerConfigurations();
        if (mcpServersResult.IsFailure)
        {
            McpConfigurationLoadFailed(_logger, mcpServersResult.Error, null);
            return mcpServersResult.Error;
        }

        var enabledMcpServers = mcpServersResult.Value;
        UsingEnabledMcpServers(_logger, enabledMcpServers.Count, null);

        // Create AI request with all enabled MCP servers and conversation context
        var messageWithContext = string.IsNullOrEmpty(conversationContext) 
            ? command.Message 
            : $"Previous conversation:\n{conversationContext}\n\nUser: {command.Message}";

        var aiRequest = new AiRequest(
            Message: messageWithContext,
            McpConfigs: enabledMcpServers.Count > 0 ? enabledMcpServers : null,
            PreviousResponseId: command.PreviousResponseId);

        return (aiRequest, enabledMcpServers.Count);
    }
}