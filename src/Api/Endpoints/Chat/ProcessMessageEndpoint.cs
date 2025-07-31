using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.Api.Endpoints.Chat;

/// <summary>
/// Endpoint for processing chat messages with direct MCP support
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ProcessMessageEndpoint : ControllerBase
{
    private readonly IMediator _mediator;

    public ProcessMessageEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Process a chat message with optional MCP tool integration
    /// </summary>
    /// <param name="request">Message processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed message response</returns>
    [HttpPost("process")]
    [AllowAnonymous]
    [ProducesResponseType<Contracts.Chat.ProcessMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Contracts.Chat.ProcessMessageResponse>> ProcessMessage(
        [FromBody] ProcessMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Map API request to application command
        var command = new ProcessMessageCommand(
            Message: request.Message,
            McpServerUrl: request.McpServer?.ServerUrl,
            McpHeaders: request.McpServer?.Headers,
            AllowedTools: request.McpServer?.AllowedTools,
            PreviousResponseId: request.ConversationId);

        // Execute command via MediatR
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapErrorToActionResult(result.Error);
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

        return Ok(apiResponse);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Method returns multiple different ActionResult types")]
    private ActionResult MapErrorToActionResult(Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => BadRequest(CreateProblemDetails(
                "Validation Error", 
                error.Message,
                StatusCodes.Status400BadRequest)),
            ErrorType.NotFound => NotFound(CreateProblemDetails(
                "Not Found",
                error.Message,
                StatusCodes.Status404NotFound)),
            ErrorType.ExternalService => StatusCode(
                StatusCodes.Status502BadGateway,
                CreateProblemDetails(
                    "External Service Error",
                    error.Message,
                    StatusCodes.Status502BadGateway)),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Internal Server Error",
                    "An unexpected error occurred",
                    StatusCodes.Status500InternalServerError))
        };
    }

    private static ProblemDetails CreateProblemDetails(string title, string detail, int statusCode) =>
        new()
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
}