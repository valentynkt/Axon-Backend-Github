using System.Diagnostics;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.Ai;

/// <summary>
/// OpenAI client implementation with direct MCP integration via Responses API
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class OpenAiClient : IAiClient
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat.Infrastructure.OpenAi");
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;
    
    // Constants for configuration values
    private const string OpenAiResponsesApiUrl = "https://api.openai.com/v1/responses";
    private const string DefaultMcpServerLabel = "mcp_server";
    private const string UnknownToolName = "unknown_tool";
    
    private static readonly JsonSerializerOptions _snakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> openAiOptions,
        ILogger<OpenAiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(openAiOptions);
        
        _httpClient = httpClient;
        _options = openAiOptions.Value;
        _logger = logger;
        
        // Configure HttpClient for OpenAI API
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Configure timeout from options
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
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
                "Processing message with OpenAI Responses API (Direct MCP) using model {Model} and {McpServerCount} MCP servers",
                _options.Model,
                request.McpConfigs?.Count ?? 0);

            // Build and execute OpenAI Responses API request with Direct MCP
            var responsesApiResponse = await ExecuteResponsesApiRequest(request, activity, cancellationToken);
            stopwatch.Stop();

            // Parse tool executions from the response
            var toolExecutions = ExtractToolExecutions(responsesApiResponse, stopwatch.Elapsed, activity);

            var response = new AiResponse(
                Content: responsesApiResponse.OutputText ?? string.Empty,
                ResponseId: responsesApiResponse.Id ?? Guid.NewGuid().ToString(),
                ToolExecutions: toolExecutions);

            activity?.SetTag("response.length", response.Content.Length);
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("response.id", response.ResponseId);
            
            _logger.LogInformation(
                "Successfully processed message in {Duration}ms with {ToolCount} tool executions using Direct MCP",
                stopwatch.ElapsedMilliseconds,
                toolExecutions?.Length ?? 0);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HandleProcessingException(ex, stopwatch, activity);
        }
    }

    /// <summary>
    /// Handle exceptions that occur during message processing
    /// </summary>
    private Result<AiResponse> HandleProcessingException(Exception ex, Stopwatch stopwatch, Activity? activity)
    {
        stopwatch.Stop();
        activity?.SetTag("error", true);
        
        _logger.LogError(ex,
            "Failed to process message with Direct MCP after {Duration}ms: {Error}",
            stopwatch.ElapsedMilliseconds,
            ex.Message);

        return ex switch
        {
            HttpRequestException => ChatErrors.AiClient.Unavailable,
            TimeoutException => ChatErrors.AiClient.ProcessingTimeout,
            UnauthorizedAccessException => ChatErrors.AiClient.Unavailable,
            JsonException => ChatErrors.AiClient.InvalidResponse,
            _ => ChatErrors.AiClient.InvalidResponse
        };
    }

    /// <summary>
    /// Execute OpenAI Responses API request with Direct MCP integration
    /// </summary>
    private async Task<ResponsesApiResponse> ExecuteResponsesApiRequest(
        AiRequest request, 
        Activity? activity,
        CancellationToken cancellationToken)
    {
        // Build tools array with MCP servers
        var tools = new List<object>();
        
        if (_options.McpEnabled && request.McpConfigs?.Count > 0)
        {
            foreach (var mcpConfig in request.McpConfigs)
            {
                var mcpTool = CreateMcpTool(mcpConfig);
                tools.Add(mcpTool);
                
                activity?.SetTag($"mcp.server.{mcpConfig.ServerLabel}.domain", new Uri(mcpConfig.ServerUrl).Host);
                activity?.SetTag($"mcp.server.{mcpConfig.ServerLabel}.tools_count", mcpConfig.AllowedTools?.Length ?? 0);
            }
            
            activity?.SetTag("mcp.enabled", true);
            activity?.SetTag("mcp.servers_configured", request.McpConfigs.Count);
        }

        // Build request payload
        var jsonPayload = BuildRequestPayload(request, tools);
        
        _logger.LogDebug(
            "Sending request to OpenAI Responses API with payload size {PayloadSize} bytes",
            jsonPayload.Length);

        // Execute request
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(OpenAiResponsesApiUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenAI Responses API returned error {StatusCode}",
                response.StatusCode);
                
            throw new HttpRequestException(
                $"OpenAI API returned {response.StatusCode}");
        }

        // Parse response
        var apiResponse = JsonSerializer.Deserialize<ResponsesApiResponse>(responseBody, _snakeCaseJsonOptions);
        
        if (apiResponse == null)
        {
            throw new JsonException("Failed to deserialize OpenAI Responses API response");
        }

        _logger.LogDebug(
            "Received response from OpenAI with ID {ResponseId} and {ContentLength} characters",
            apiResponse.Id,
            apiResponse.OutputText?.Length ?? 0);

        return apiResponse;
    }

    /// <summary>
    /// Build the request payload for OpenAI Responses API
    /// </summary>
    private string BuildRequestPayload(AiRequest request, List<object> tools)
    {
        var requestPayload = new
        {
            model = _options.Model,
            input = request.Message,
            tools = tools.Count > 0 ? tools.ToArray() : null,
            previous_response_id = request.PreviousResponseId,
            max_output_tokens = _options.MaxTokens,
            temperature = _options.Temperature
        };

        return JsonSerializer.Serialize(requestPayload, _snakeCaseJsonOptions);
    }

    /// <summary>
    /// Create MCP tool definition for Responses API
    /// </summary>
    private static Dictionary<string, object> CreateMcpTool(McpServerConfig mcpConfig)
    {
        var tool = new Dictionary<string, object>
        {
            ["type"] = "mcp",
            ["server_url"] = mcpConfig.ServerUrl,
            ["server_label"] = mcpConfig.ServerLabel ?? DefaultMcpServerLabel,
            ["require_approval"] = mcpConfig.RequireApproval,
            ["timeout_seconds"] = mcpConfig.TimeoutSeconds
        };

        // Add headers if configured
        if (mcpConfig.Headers?.Count > 0)
        {
            tool["headers"] = mcpConfig.Headers;
        }

        // Add allowed tools if configured
        if (mcpConfig.AllowedTools?.Length > 0)
        {
            tool["allowed_tools"] = mcpConfig.AllowedTools;
        }

        return tool;
    }

    /// <summary>
    /// Extract tool execution information from OpenAI Responses API response
    /// </summary>
    private ToolExecution[]? ExtractToolExecutions(
        ResponsesApiResponse response, 
        TimeSpan totalDuration, 
        Activity? activity)
    {
        // Check if response contains MCP tool calls
        if (response.McpCalls == null || response.McpCalls.Length == 0)
        {
            return null;
        }

        var toolExecutions = new List<ToolExecution>();
        var averageExecutionTime = TimeSpan.FromMilliseconds(totalDuration.TotalMilliseconds / response.McpCalls.Length);
        
        foreach (var mcpCall in response.McpCalls)
        {
            var toolExecution = mcpCall.Error != null
                ? ToolExecution.Failure(
                    toolName: mcpCall.ToolName ?? UnknownToolName,
                    arguments: JsonSerializer.Serialize(mcpCall.Arguments ?? new object()),
                    errorMessage: mcpCall.Error,
                    executionTime: averageExecutionTime)
                : ToolExecution.Success(
                    toolName: mcpCall.ToolName ?? UnknownToolName,
                    arguments: JsonSerializer.Serialize(mcpCall.Arguments ?? new object()),
                    result: JsonSerializer.Serialize(mcpCall.Output ?? ""),
                    executionTime: averageExecutionTime);
                
            toolExecutions.Add(toolExecution);
            
            _logger.LogDebug(
                "MCP tool execution: {ToolName} -> {Status} in ~{Duration}ms",
                mcpCall.ToolName,
                mcpCall.Error != null ? "Failed" : "Success",
                averageExecutionTime.TotalMilliseconds);
        }
        
        activity?.SetTag("tools.executed", toolExecutions.Count);
        activity?.SetTag("tools.successful", toolExecutions.Count(t => t.IsSuccess));
        activity?.SetTag("tools.failed", toolExecutions.Count(t => !t.IsSuccess));
        
        return toolExecutions.ToArray();
    }
}

/// <summary>
/// OpenAI Responses API response model
/// </summary>
internal sealed record ResponsesApiResponse(
    string? Id,
    string? OutputText,
    McpCallItem[]? McpCalls);

/// <summary>
/// MCP call item in the response
/// </summary>
internal sealed record McpCallItem(
    string? ToolName,
    object? Arguments,
    object? Output,
    string? Error);