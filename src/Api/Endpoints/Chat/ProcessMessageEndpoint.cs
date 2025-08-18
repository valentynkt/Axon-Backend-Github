// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Endpoints/Chat/ProcessMessageEndpoint.cs
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.DTOs;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using IMcpServerResolver = Axon.Modules.Chat.Application.Abstractions.IMcpServerResolver;

namespace Axon.Api.Endpoints.Chat;

/// <summary>
/// Universal chat endpoint: starts or continues a conversation depending on PreviousResponseId.
/// </summary>
public sealed class ProcessMessageEndpoint : Endpoint<ProcessMessageRequest, ProcessMessageResponse>
{
    private readonly IAiClient _aiClient;
    private readonly IMcpServerResolver _mcpResolver;
    private readonly ILogger<ProcessMessageEndpoint> _logger;

    public ProcessMessageEndpoint(
        IAiClient aiClient,
        IMcpServerResolver mcpResolver,
        ILogger<ProcessMessageEndpoint> logger)
    {
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
        _mcpResolver = mcpResolver ?? throw new ArgumentNullException(nameof(mcpResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Configure()
    {
        Post("/api/v1/chat/turns");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Send a chat message (start or continue a conversation).";
            s.Description =
                "If previousResponseId is null, a new conversation is started. " +
                "If provided, the message is appended to the conversation based on the given response id.";
            s.Responses[200] = "Message processed; assistant response returned.";
            s.Responses[400] = "Bad request (validation error).";
            s.Responses[500] = "Processing failed.";
        });

        Tags("Chat");
    }

    public override async Task HandleAsync(ProcessMessageRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Processing chat turn. Continuation: {IsContinuation}",
            !string.IsNullOrWhiteSpace(req.PreviousResponseId));

        try
        {
            // Resolve MCP server configuration (if requested)
            McpServerConfig[]? mcpConfigs = null;

            if (req.UseMcpServers)
            {
                if (!string.IsNullOrWhiteSpace(req.McpServerUrl))
                {
                    mcpConfigs = new[]
                    {
                        new McpServerConfig(
                            ServerUrl: req.McpServerUrl!,
                            ServerLabel: "custom-mcp",
                            Headers: null,
                            AllowedTools: null,
                            RequireApproval: false,
                            TimeoutSeconds: 30)
                    };
                }
                else
                {
                    mcpConfigs = await _mcpResolver.ResolveServersAsync(ct);
                    _logger.LogDebug("Resolved {Count} MCP servers from configuration.", mcpConfigs.Length);
                }
            }

            // Build request to AI client
            var aiRequest = new AiRequest(
                Message: req.Message,
                McpConfigs: mcpConfigs,
                PreviousResponseId: req.PreviousResponseId);

            // The IAiClient internally decides between "start" vs "append" using PreviousResponseId.
            var result = await _aiClient.ProcessMessageAsync(aiRequest, ct);

            if (result.IsFailure)
            {
                var error = result.Error;
                _logger.LogError("AI processing failed. Code={Code}, Message={Message}", error.Code, error.Message);

                await HttpContext.Response.SendAsync(new ProcessMessageResponse
                {
                    Success = false,
                    Content = string.Empty,
                    ResponseId = null,
                    Timestamp = DateTime.UtcNow,
                    Message = $"Processing failed: {error.Message}"
                }, 500, cancellation: ct);

                return;
            }

            var value = result.Value;
            await HttpContext.Response.SendAsync(new ProcessMessageResponse
            {
                Success = true,
                Content = value.Content,
                ResponseId = value.ResponseId,
                Timestamp = DateTime.UtcNow
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing chat turn.");

            await HttpContext.Response.SendAsync(new ProcessMessageResponse
            {
                Success = false,
                Content = string.Empty,
                ResponseId = null,
                Timestamp = DateTime.UtcNow,
                Message = $"Unexpected error: {ex.Message}"
            }, 500, cancellation: ct);
        }
    }
}
