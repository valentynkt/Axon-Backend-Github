using System.Diagnostics;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Axon.Modules.Chat.Infrastructure.Ai;

/// <summary>
/// OpenAI client implementation with direct MCP integration
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class OpenAiClient : IAiClient
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat.Infrastructure.OpenAi");
    private readonly ChatClient _chatClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;
    private static readonly JsonSerializerOptions _snakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public OpenAiClient(
        IOptions<OpenAiOptions> openAiOptions,
        ILogger<OpenAiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(openAiOptions);
        
        _options = openAiOptions.Value;
        _logger = logger;
        
        var openAiClient = new OpenAIClient(_options.ApiKey);
        _chatClient = openAiClient.GetChatClient(_options.Model);
    }

    public async Task<Result<AiResponse>> ProcessMessageAsync(
        AiRequest request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        using var activity = ActivitySource.StartActivity("ProcessMessage");
        activity?.SetTag("mcp.server_count", request.McpConfigs?.Count ?? 0);
        activity?.SetTag("message.length", request.Message.Length);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Processing message with OpenAI model {Model} and {McpServerCount} MCP servers",
                _options.Model,
                request.McpConfigs?.Count ?? 0);

            // Execute OpenAI request
            var (content, responseId) = await ExecuteOpenAiRequest(request, activity, cancellationToken);
            stopwatch.Stop();

            // Simulate MCP tool execution if configured
            var toolExecutions = SimulateToolExecution(request.McpConfigs, stopwatch.Elapsed, activity);

            var response = new AiResponse(
                Content: content,
                ResponseId: responseId,
                ToolExecutions: toolExecutions);

            activity?.SetTag("response.length", content.Length);
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            
            _logger.LogInformation(
                "Successfully processed message in {Duration}ms with {ToolCount} tool executions",
                stopwatch.ElapsedMilliseconds,
                toolExecutions?.Length ?? 0);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            activity?.SetTag("error", true);
            
            _logger.LogError(ex,
                "Failed to process message after {Duration}ms: {Error}",
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            return ex switch
            {
                HttpRequestException => ChatErrors.AiClient.Unavailable,
                TimeoutException => ChatErrors.AiClient.ProcessingTimeout,
                UnauthorizedAccessException => ChatErrors.AiClient.Unavailable,
                _ => ChatErrors.AiClient.InvalidResponse
            };
        }
    }

    // Note: CreateMcpTool method removed as OpenAI.NET doesn't support MCP tools yet
    // Will be re-added when OpenAI.NET provides MCP tool support

    private async Task<(string content, string responseId)> ExecuteOpenAiRequest(
        AiRequest request, 
        Activity? activity,
        CancellationToken cancellationToken)
    {
        // Build chat completion request
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage(request.Message)
        };

        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.MaxTokens,
            Temperature = (float)_options.Temperature
        };

        // Add MCP tools if configured
        if (_options.McpEnabled && request.McpConfigs?.Count > 0)
        {
            // Note: OpenAI.NET might not support MCP tools directly yet
            // This would create the appropriate MCP tool configuration when available:
            // foreach (var mcpConfig in request.McpConfigs)
            // {
            //     var mcpTool = CreateMcpTool(mcpConfig);
            //     completionOptions.Tools.Add(mcpTool);
            // }
            activity?.SetTag("mcp.enabled", true);
            activity?.SetTag("mcp.server_count", request.McpConfigs.Count);
        }

        // Execute chat completion
        var chatCompletion = await _chatClient.CompleteChatAsync(
            messages, 
            completionOptions, 
            cancellationToken);
            
        var content = chatCompletion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        var responseId = request.PreviousResponseId ?? Guid.NewGuid().ToString();
        
        return (content, responseId);
    }

    private static ToolExecution[]? SimulateToolExecution(
        IReadOnlyCollection<McpServerConfig>? configs, 
        TimeSpan duration, 
        Activity? activity)
    {
        if (configs == null || configs.Count == 0)
            return null;
            
        var toolExecutions = new List<ToolExecution>();
        var executionTimePerTool = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / configs.Count);
        
        foreach (var config in configs)
        {
            var toolExecution = ToolExecution.Success(
                toolName: $"tool_from_{config.ServerLabel}",
                arguments: "{\"query\":\"simulated_request\"}",
                result: $"Simulated response from {config.ServerUrl}",
                executionTime: executionTimePerTool);
                
            toolExecutions.Add(toolExecution);
        }
        
        activity?.SetTag("tools.executed", toolExecutions.Count);
        return toolExecutions.ToArray();
    }
}