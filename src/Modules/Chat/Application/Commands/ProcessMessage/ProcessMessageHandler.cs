using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Coordinator for processing chat messages - handles only orchestration and coordination
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class ProcessMessageHandler : IRequestHandler<ProcessMessageCommand, Result<ProcessMessageResponse>>
{
    private readonly IAiClient _aiClient;
    private readonly IMessageRequestBuilder _requestBuilder;
    private readonly IResponseMappingService _responseMappingService;
    private readonly ILogger<ProcessMessageHandler> _logger;

    public ProcessMessageHandler(
        IAiClient aiClient,
        IMessageRequestBuilder requestBuilder,
        IResponseMappingService responseMappingService,
        ILogger<ProcessMessageHandler> logger)
    {
        _aiClient = aiClient;
        _requestBuilder = requestBuilder;
        _responseMappingService = responseMappingService;
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

        // Build AI request with MCP configurations using dedicated service
        var aiRequestResult = _requestBuilder.BuildAiRequest(request);
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
        
        // Map AI response to API response using dedicated service
        var response = _responseMappingService.MapToApiResponse(aiResponse);

        _logger.LogInformation(
            "Successfully processed message with {ToolCount} tool executions using {McpServerCount} MCP servers",
            response.ToolExecutions?.Length ?? 0,
            mcpServerCount);

        return response;
    }

}