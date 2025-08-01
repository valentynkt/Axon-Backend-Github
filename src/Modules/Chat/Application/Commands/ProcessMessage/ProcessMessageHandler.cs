using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Handler for processing chat messages with direct MCP support
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class ProcessMessageHandler : IRequestHandler<ProcessMessageCommand, Result<ProcessMessageResponse>>
{
    private readonly IAiClient _aiClient;
    private readonly IMcpServerResolver _mcpServerResolver;
    private readonly ILogger<ProcessMessageHandler> _logger;

    public ProcessMessageHandler(
        IAiClient aiClient,
        IMcpServerResolver mcpServerResolver,
        ILogger<ProcessMessageHandler> logger)
    {
        _aiClient = aiClient;
        _mcpServerResolver = mcpServerResolver;
        _logger = logger;
    }

    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        // POLICY FIX: Use Result pattern instead of throwing exceptions
        if (request is null)
            return Error.Validation("ProcessMessageCommand cannot be null");
        
        _logger.LogInformation(
            "Processing message with length {MessageLength}",
            request.Message.Length);

        // Build AI request with MCP configurations
        var aiRequestResult = BuildAiRequest(request);
        if (aiRequestResult.IsFailure)
        {
            return aiRequestResult.Error;
        }

        var (aiRequest, mcpServerCount) = aiRequestResult.Value;

        // Process message via AI client
        var processResult = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        if (processResult.IsFailure)
        {
            _logger.LogError(
                "AI client failed to process message: {Error}",
                processResult.Error);
            return processResult.Error;
        }

        var aiResponse = processResult.Value;
        
        // Map AI response to API response
        var response = MapToApiResponse(aiResponse);

        _logger.LogInformation(
            "Successfully processed message with {ToolCount} tool executions using {McpServerCount} MCP servers",
            response.ToolExecutions?.Length ?? 0,
            mcpServerCount);

        return response;
    }

    private Result<(AiRequest Request, int McpServerCount)> BuildAiRequest(ProcessMessageCommand request)
    {
        // Get all enabled MCP servers from configuration
        var mcpServersResult = _mcpServerResolver.GetEnabledServerConfigurations();
        if (mcpServersResult.IsFailure)
        {
            _logger.LogError(
                "Failed to load MCP server configurations: {Error}",
                mcpServersResult.Error);
            return mcpServersResult.Error;
        }

        var enabledMcpServers = mcpServersResult.Value;
        _logger.LogDebug("Using {McpServerCount} enabled MCP servers", enabledMcpServers.Count);

        // Create AI request with all enabled MCP servers
        var aiRequest = new AiRequest(
            Message: request.Message,
            McpConfigs: enabledMcpServers.Count > 0 ? enabledMcpServers : null,
            PreviousResponseId: request.PreviousResponseId);

        return (aiRequest, enabledMcpServers.Count);
    }

    private static ProcessMessageResponse MapToApiResponse(AiResponse aiResponse)
    {
        // Generate conversation ID if not provided in response
        var conversationId = aiResponse.ResponseId ?? ConversationId.New().ToString();
        
        // Map tool executions to summaries
        var toolSummaries = aiResponse.ToolExecutions?.Select(tool =>
            new ToolExecutionSummary(
                ToolName: tool.ToolName,
                Success: tool.IsSuccess,
                Duration: tool.ExecutionTime)).ToArray();

        return new ProcessMessageResponse(
            Response: aiResponse.Content,
            ConversationId: conversationId,
            ToolExecutions: toolSummaries);
    }
}