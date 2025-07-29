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
    private readonly ILogger<ProcessMessageHandler> _logger;

    public ProcessMessageHandler(
        IAiClient aiClient,
        ILogger<ProcessMessageHandler> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation(
            "Processing message with length {MessageLength} and MCP server {McpServerUrl}",
            request.Message.Length,
            request.McpServerUrl ?? "none");

        // Create MCP configuration if provided
        var mcpConfig = CreateMcpConfigurationFromRequest(request);

        // Create AI request
        var aiRequest = new AiRequest(
            Message: request.Message,
            McpConfig: mcpConfig,
            PreviousResponseId: request.PreviousResponseId);

        // Process message via AI client
        var processResult = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        if (processResult.IsFailure)
        {
            _logger.LogError(
                "AI client failed to process message: {Error}",
                processResult.Error);
            return processResult.Error;
        }

        var aiClientResponse = processResult.Value;
        
        // Map AI response to API response
        var response = MapToApiResponse(aiClientResponse);

        _logger.LogInformation(
            "Successfully processed message with {ToolCount} tool executions",
            response.ToolExecutions?.Length ?? 0);

        return response;
    }

    private static McpServerConfig? CreateMcpConfigurationFromRequest(ProcessMessageCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.McpServerUrl))
            return null;

        return new McpServerConfig(
            ServerUrl: request.McpServerUrl,
            ServerLabel: "User-provided MCP Server",
            Headers: request.McpHeaders,
            AllowedTools: request.AllowedTools,
            RequireApproval: false);
    }

    private static ProcessMessageResponse MapToApiResponse(AiResponse aiClientResponse)
    {
        // Generate conversation ID if not provided in response
        var conversationId = aiClientResponse.ResponseId ?? ConversationId.New().ToString();
        
        // Map tool executions to summaries
        var toolSummaries = aiClientResponse.ToolExecutions?.Select(tool =>
            new ToolExecutionSummary(
                ToolName: tool.ToolName,
                Success: tool.IsSuccess,
                Duration: tool.ExecutionTime)).ToArray();

        return new ProcessMessageResponse(
            Response: aiClientResponse.Content,
            ConversationId: conversationId,
            ToolExecutions: toolSummaries);
    }
}