using Axon.Api.Common.ErrorHandling;
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Axon.Api.Endpoints.Chat.ProcessMessage;

/// <summary>
/// FastEndpoints implementation for processing chat messages with direct MCP support
/// </summary>
public sealed class ProcessMessageEndpoint : Endpoint<ProcessMessageRequest, Contracts.Chat.ProcessMessageResponse>
{
    private readonly IMediator _mediator;
    private readonly IErrorMapper _errorMapper;
    private readonly ILogger<ProcessMessageEndpoint> _logger;

    public ProcessMessageEndpoint(IMediator mediator, IErrorMapper errorMapper, ILogger<ProcessMessageEndpoint> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post("/api/chat/process");
        AllowAnonymous();
        
        Summary(ConfigureOpenApiExamples);
        
        Tags("Chat");
    }

    public override async Task HandleAsync(ProcessMessageRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        using var activity = Activity.Current?.Source.StartActivity("ProcessMessage");
        activity?.SetTag("message.length", req.Message.Length.ToString());
        activity?.SetTag("conversation.id", req.ConversationId);

        _logger.LogInformation(
            "Processing chat message with length {MessageLength} for conversation {ConversationId}",
            req.Message.Length,
            req.ConversationId);

        // Map API request to application command
        var command = new ProcessMessageCommand(
            Message: req.Message,
            PreviousResponseId: req.ConversationId);

        // Execute command via MediatR
        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            await WriteErrorResponseAsync(result.Error, ct);
            return;
        }

        var response = result.Value;
        
        // Map application response to API response
        var apiResponse = new Contracts.Chat.ProcessMessageResponse(
            Response: response.Response,
            ConversationId: response.ConversationId ?? Guid.NewGuid().ToString(),
            ToolExecutions: response.ToolExecutions?.Select(tool =>
                new Contracts.Chat.ToolExecutionResponse(
                    ToolName: tool.ToolName,
                    Success: tool.Success,
                    DurationMs: (int)tool.Duration.TotalMilliseconds)).ToArray());

        _logger.LogInformation(
            "Successfully processed chat message with {ToolExecutionCount} tool executions for conversation {ConversationId}",
            apiResponse.ToolExecutions?.Length ?? 0,
            apiResponse.ConversationId);

        await HttpContext.Response.WriteAsJsonAsync(apiResponse, ct);
    }

    private async Task WriteErrorResponseAsync(Shared.Common.Error error, CancellationToken ct)
    {
        _logger.LogWarning(
            "Failed to process chat message: {Error}",
            error);
            
        var statusCode = _errorMapper.MapToStatusCode(error);
        var problemDetails = _errorMapper.MapToProblemDetails(error);
        
        HttpContext.Response.StatusCode = statusCode;
        await HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
    }

    private static void ConfigureOpenApiExamples(Summary s)
    {
        s.Summary = "Process a chat message with automatic MCP tool integration";
        s.Description = """
            Processes a user message through the AI chat system with automatic MCP tool integration.
            All enabled MCP servers from configuration are automatically available.
            Built with FastEndpoints for high performance and clean API design.
            """;
        s.ExampleRequest = new ProcessMessageRequest(
            Message: "Hello, how can you help me today?",
            ConversationId: null);
        s.ResponseExamples[200] = new Contracts.Chat.ProcessMessageResponse(
            Response: "Hello! I'm here to help you with any questions you might have.",
            ConversationId: Guid.NewGuid().ToString(),
            ToolExecutions: null);
        s.Responses[200] = "Message processed successfully";
        s.Responses[400] = "Invalid request data";
        s.Responses[500] = "Internal server error";
        s.Responses[502] = "External service error";
    }
}