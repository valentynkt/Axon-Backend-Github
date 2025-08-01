using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Service responsible for building HTTP requests and payloads for OpenAI API
/// </summary>
public sealed class HttpRequestBuilder : IHttpRequestBuilder
{
    private readonly OpenAiOptions _options;
    private readonly IPayloadSerializer _payloadSerializer;
    private readonly ILogger<HttpRequestBuilder> _logger;

    // Constants for configuration values
    private const string OpenAiResponsesApiUrl = "https://api.openai.com/v1/responses";
    private const string DefaultMcpServerLabel = "mcp_server";

    public HttpRequestBuilder(
        IOptions<OpenAiOptions> openAiOptions,
        IPayloadSerializer payloadSerializer,
        ILogger<HttpRequestBuilder> logger)
    {
        _options = openAiOptions.Value;
        _payloadSerializer = payloadSerializer;
        _logger = logger;
    }

    /// <summary>
    /// Configures HttpClient with OpenAI authentication and headers
    /// </summary>
    /// <param name="httpClient">HttpClient to configure</param>
    public void ConfigureHttpClient(HttpClient httpClient)
    {
        // Configure HttpClient for OpenAI API
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Configure timeout from options
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// Builds HTTP content for OpenAI Responses API request
    /// </summary>
    /// <param name="request">AI request</param>
    /// <param name="activity">Activity for tracing</param>
    /// <returns>HTTP content ready for sending</returns>
    public StringContent BuildRequestContent(AiRequest request, Activity? activity)
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
            "Sending request to OpenAI Responses API with payload size {PayloadSize} bytes. Payload: {Payload}",
            jsonPayload.Length,
            jsonPayload);

        return new StringContent(jsonPayload, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Gets the OpenAI API URL
    /// </summary>
    /// <returns>API URL</returns>
    public string GetApiUrl() => OpenAiResponsesApiUrl;

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

        if (_options.Temperature >= 0.0 && _options.Temperature <= 2.0)
        {
            requestPayload["temperature"] = _options.Temperature;
        }

        // Note: previous_response_id removed as it may not be supported by the API
        // TODO: Re-add when conversation context is officially supported

        return _payloadSerializer.Serialize(requestPayload);
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
}