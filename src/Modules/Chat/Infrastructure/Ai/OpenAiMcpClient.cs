using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.Ai;

/// <summary>
/// Ultra-simple proof of concept for OpenAI Direct MCP integration via Responses API
/// This is a minimal implementation focusing on getting the basic flow working
/// </summary>
public sealed class OpenAiMcpClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiMcpClient> _logger;
    private readonly OpenAiOptions _options;
    private const string ResponsesApiUrl = "https://api.openai.com/v1/responses";

    public OpenAiMcpClient(
        HttpClient httpClient,
        ILogger<OpenAiMcpClient> logger,
        IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        ConfigureHttpClient();
    }

    public async Task<Result<AiResponse>> ProcessMessageAsync(
        AiRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing message with Direct MCP - POC version");
            
            // Build the request payload
            var requestPayload = BuildRequestPayload(request);
            
            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestPayload);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Sending request to OpenAI Responses API: {Content}", jsonContent);
            
            // Send the request
            var response = await _httpClient.PostAsync(ResponsesApiUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            _logger.LogDebug("Received response: {Response}", responseBody);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return AiErrors.ServiceUnavailable;
            }
            
            // Parse the response
            var aiResponse = ParseResponse(responseBody);
            
            _logger.LogInformation("Successfully processed message with Direct MCP");
            
            return Result<AiResponse>.Success(aiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message with Direct MCP");
            return AiErrors.RequestFailed;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Axon-Chat-POC/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    private object BuildRequestPayload(AiRequest request)
    {
        // Build the base payload according to OpenAI Responses API format
        var payload = new Dictionary<string, object>
        {
            ["model"] = _options.Model,
            ["input"] = request.Message
        };
        
        // Add tools if MCP configs are provided
        var tools = BuildMcpTools(request.McpConfigs);
        if (tools != null && tools.Length > 0)
        {
            payload["tools"] = tools;
        }
        
        // Add previous_response_id if provided for conversation continuity
        if (!string.IsNullOrEmpty(request.PreviousResponseId))
        {
            payload["previous_response_id"] = request.PreviousResponseId;
        }
        
        // Add optional parameters that are supported by Responses API
        if (_options.Temperature > 0)
        {
            payload["temperature"] = _options.Temperature;
        }
        
        if (_options.MaxTokens > 0)
        {
            payload["max_output_tokens"] = _options.MaxTokens;
        }
        
        return payload;
    }

    private object[]? BuildMcpTools(IReadOnlyCollection<McpServerConfig>? mcpConfigs)
    {
        if (mcpConfigs == null || mcpConfigs.Count == 0)
        {
            _logger.LogDebug("No MCP servers configured for this request");
            return null;
        }
        
        var tools = new List<object>();
        
        foreach (var config in mcpConfigs)
        {
            // Build MCP tool according to OpenAI Responses API specification
            var mcpTool = new Dictionary<string, object>
            {
                ["type"] = "mcp",
                ["server_url"] = config.ServerUrl,
                ["server_label"] = config.ServerLabel ?? "MCP Server",
                ["require_approval"] = config.RequireApproval ? "always" : "never"
            };
            
            // Add headers if provided
            if (config.Headers != null && config.Headers.Count > 0)
            {
                mcpTool["headers"] = config.Headers;
            }
            
            tools.Add(mcpTool);
            
            _logger.LogDebug("Added MCP server: {Label} at {Url} with approval: {Approval}", 
                config.ServerLabel, config.ServerUrl, mcpTool["require_approval"]);
        }
        
        return tools.ToArray();
    }

    private AiResponse ParseResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            // Extract response ID
            var responseId = root.TryGetProperty("id", out var idElem) 
                ? idElem.GetString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();
            
            // Extract text content from output
            var textContent = ExtractTextContent(root);
            
            // For POC, we're not parsing tool executions yet
            // This can be enhanced later
            
            return new AiResponse(
                Content: textContent,
                ResponseId: responseId,
                ToolExecutions: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response");
            // Return a minimal response on parse error
            return new AiResponse(
                Content: "Error parsing response",
                ResponseId: Guid.NewGuid().ToString(),
                ToolExecutions: null);
        }
    }

    private string ExtractTextContent(JsonElement root)
    {
        // Try to get output array
        if (!root.TryGetProperty("output", out var outputArray))
        {
            _logger.LogWarning("No output property in response");
            return string.Empty;
        }
        
        // Look for message type items in output
        foreach (var outputItem in outputArray.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("type", out var typeElem))
                continue;
                
            if (typeElem.GetString() != "message")
                continue;
                
            if (!outputItem.TryGetProperty("content", out var contentElem))
                continue;
                
            // Try to parse content as array of message items
            if (contentElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var contentItem in contentElem.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElem))
                    {
                        var text = textElem.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            _logger.LogDebug("Extracted text content from response");
                            return text;
                        }
                    }
                }
            }
        }
        
        _logger.LogWarning("Could not extract text content from response");
        return string.Empty;
    }
}