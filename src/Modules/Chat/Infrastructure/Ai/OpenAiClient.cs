using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
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
    private readonly IErrorMappingService _errorMappingService;
    private readonly IToolExecutionService _toolExecutionService;
    private readonly IJsonSerializationService _jsonSerializationService;
    private readonly IActivityTracker _activityTracker;
    
    // Constants for configuration values
    private const string OpenAiResponsesApiUrl = "https://api.openai.com/v1/responses";
    private const string DefaultMcpServerLabel = "mcp_server";

    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> openAiOptions,
        ILogger<OpenAiClient> logger,
        IErrorMappingService errorMappingService,
        IToolExecutionService toolExecutionService,
        IJsonSerializationService jsonSerializationService,
        IActivityTracker activityTracker)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(openAiOptions);
        ArgumentNullException.ThrowIfNull(errorMappingService);
        ArgumentNullException.ThrowIfNull(toolExecutionService);
        ArgumentNullException.ThrowIfNull(jsonSerializationService);
        ArgumentNullException.ThrowIfNull(activityTracker);
        
        _httpClient = httpClient;
        _options = openAiOptions.Value;
        _logger = logger;
        _errorMappingService = errorMappingService;
        _toolExecutionService = toolExecutionService;
        _jsonSerializationService = jsonSerializationService;
        _activityTracker = activityTracker;
        
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
        _activityTracker.SetTags(activity, 
            ("mcp.server_count", request.McpConfigs?.Count ?? 0),
            ("message.length", request.Message.Length));
        
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

            // Extract text content and tool executions from the response
            var textContent = ExtractTextContent(responsesApiResponse);
            var toolExecutions = ExtractToolExecutions(responsesApiResponse);

            var response = new AiResponse(
                Content: textContent,
                ResponseId: responsesApiResponse.Id ?? Guid.NewGuid().ToString(),
                ToolExecutions: toolExecutions);

            _activityTracker.SetTags(activity, 
                ("response.length", response.Content.Length),
                ("duration.ms", stopwatch.ElapsedMilliseconds),
                ("response.id", response.ResponseId));
            
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
        _activityTracker.MarkError(activity, ex);
        
        _logger.LogError(ex,
            "Failed to process message with Direct MCP after {Duration}ms: {Error}",
            stopwatch.ElapsedMilliseconds,
            ex.Message);

        return _errorMappingService.MapProcessingException(ex);
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
                
                _activityTracker.SetTags(activity,
                    ($"mcp.server.{mcpConfig.ServerLabel}.domain", new Uri(mcpConfig.ServerUrl).Host),
                    ($"mcp.server.{mcpConfig.ServerLabel}.tools_count", mcpConfig.AllowedTools?.Length ?? 0));
            }
            
            _activityTracker.SetTags(activity,
                ("mcp.enabled", true),
                ("mcp.servers_configured", request.McpConfigs.Count));
        }

        // Build request payload
        var jsonPayload = BuildRequestPayload(request, tools);
        
        _logger.LogDebug(
            "Sending request to OpenAI Responses API with payload size {PayloadSize} bytes. Payload: {Payload}",
            jsonPayload.Length,
            jsonPayload);

        // Execute request
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(OpenAiResponsesApiUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenAI Responses API returned error {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);
                
            throw new HttpRequestException(
                $"OpenAI API returned {response.StatusCode}: {responseBody}");
        }

        // Parse response
        var apiResponse = _jsonSerializationService.DeserializeFromSnakeCase<ResponsesApiResponse>(responseBody);
        
        if (apiResponse == null)
        {
            throw new JsonException("Failed to deserialize OpenAI Responses API response");
        }

        var textContent = ExtractTextContent(apiResponse);
        _logger.LogDebug(
            "Received response from OpenAI with ID {ResponseId} and {ContentLength} characters",
            apiResponse.Id,
            textContent.Length);

        return apiResponse;
    }

    /// <summary>
    /// Build the request payload for OpenAI Responses API
    /// </summary>
    private string BuildRequestPayload(AiRequest request, List<object> tools)
    {
        // Build base payload - only include supported parameters
        var requestPayload = new Dictionary<string, object>
        {
            ["model"] = _options.Model,
            ["input"] = request.Message
        };

        // Add tools if any are configured
        if (tools.Count > 0)
        {
            requestPayload["tools"] = tools.ToArray();
        }

        // Add optional parameters if they have valid values
        if (_options.MaxTokens > 0)
        {
            requestPayload["max_output_tokens"] = _options.MaxTokens;
        }

        // Only include temperature for models that support it (not o1/o4 models)
        if (_options.Temperature >= 0.0 && _options.Temperature <= 2.0 && !IsReasoningModel(_options.Model))
        {
            requestPayload["temperature"] = _options.Temperature;
        }

        // Note: previous_response_id removed as it may not be supported by the API
        // TODO: Re-add when conversation context is officially supported

        return _jsonSerializationService.SerializeToSnakeCase(requestPayload);
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
            ["require_approval"] = mcpConfig.RequireApproval ? "always" : "never"
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

        // Note: timeout_seconds removed as it may not be supported
        // The timeout is typically handled at the HTTP client level

        return tool;
    }

    /// <summary>
    /// Extract text content from OpenAI Responses API response
    /// </summary>
    private string ExtractTextContent(ResponsesApiResponse response)
    {
        if (response.Output == null || response.Output.Length == 0)
        {
            return string.Empty;
        }

        // Look for message type output items
        foreach (var outputItem in response.Output)
        {
            if (outputItem.Type == "message" && outputItem.Content != null)
            {
                try
                {
                    // Try to deserialize as array of message content items
                    var jsonElement = (JsonElement)outputItem.Content;
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var contentItem in jsonElement.EnumerateArray())
                        {
                            if (contentItem.TryGetProperty("text", out var textProperty))
                            {
                                return textProperty.GetString() ?? string.Empty;
                            }
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse message content from output item");
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extract tool execution information from OpenAI Responses API response
    /// </summary>
    private ToolExecution[]? ExtractToolExecutions(ResponsesApiResponse response)
    {
        if (response.Output == null || response.Output.Length == 0)
        {
            return null;
        }

        foreach (var outputItem in response.Output)
        {
            if (outputItem.Type == "mcp_list_tools" && outputItem.Content != null)
            {
                // Parse MCP tool list content - for now, we just log it as no actual calls were made
                _logger.LogDebug("Received MCP tool list in response");
            }
            // TODO: Add support for other MCP output types like tool call results
        }

        // For now, return null as we're only seeing tool lists, not actual tool executions
        // This will be expanded when we handle actual tool call results
        return null;
    }

    /// <summary>
    /// Check if the model is a reasoning model (o1/o4 series) that doesn't support temperature parameter
    /// </summary>
    /// <param name="model">The model name</param>
    /// <returns>True if it's a reasoning model</returns>
    private static bool IsReasoningModel(string model)
    {
        return model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
    }
}