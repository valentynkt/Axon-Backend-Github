using System.Diagnostics;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Types;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApplicationActivityTracker = Axon.Modules.Chat.Application.Abstractions.IActivityTracker;

namespace Axon.Modules.Chat.Infrastructure.Ai;

/// <summary>
/// OpenAI client implementation with direct MCP integration via Responses API
/// Refactored to use SRP-compliant services for request building and response parsing
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simplified logging for readability")]
public sealed class OpenAiClient : IAiClient
{
    private static readonly ActivitySource ActivitySource = new("Axon.Chat.Infrastructure.OpenAi");
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly IErrorMappingService _errorMappingService;
    private readonly ApplicationActivityTracker _activityTracker;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IResponseParser _responseParser;

    public OpenAiClient(
        HttpClient httpClient,
        ILogger<OpenAiClient> logger,
        IErrorMappingService errorMappingService,
        ApplicationActivityTracker activityTracker,
        IHttpRequestBuilder requestBuilder,
        IResponseParser responseParser)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(errorMappingService);
        ArgumentNullException.ThrowIfNull(activityTracker);
        ArgumentNullException.ThrowIfNull(requestBuilder);
        ArgumentNullException.ThrowIfNull(responseParser);
        
        _httpClient = httpClient;
        _logger = logger;
        _errorMappingService = errorMappingService;
        _activityTracker = activityTracker;
        _requestBuilder = requestBuilder;
        _responseParser = responseParser;
        
        // Configure HttpClient using the extracted service
        _requestBuilder.ConfigureHttpClient(_httpClient);
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
                "Processing message with OpenAI Responses API (Direct MCP) with {McpServerCount} MCP servers",
                request.McpConfigs?.Count ?? 0);

            // Execute request using extracted services
            var responsesApiResponse = await ExecuteRequestAsync(request, activity, cancellationToken);
            stopwatch.Stop();

            // Extract content and create response
            var response = CreateAiResponse(responsesApiResponse);

            _activityTracker.SetTags(activity, 
                ("response.length", response.Content.Length),
                ("duration.ms", stopwatch.ElapsedMilliseconds),
                ("response.id", response.ResponseId));
            
            _logger.LogInformation(
                "Successfully processed message in {Duration}ms with {ToolCount} tool executions",
                stopwatch.ElapsedMilliseconds,
                response.ToolExecutions?.Length ?? 0);

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
            "Failed to process message after {Duration}ms: {Error}",
            stopwatch.ElapsedMilliseconds,
            ex.Message);

        return _errorMappingService.MapProcessingException(ex);
    }

    /// <summary>
    /// Execute request using extracted services for cleaner separation of concerns
    /// </summary>
    private async Task<ResponsesApiResponse> ExecuteRequestAsync(
        AiRequest request, 
        Activity? activity,
        CancellationToken cancellationToken)
    {
        // Build request content using extracted service
        using var content = _requestBuilder.BuildRequestContent(request, activity);
        
        // Execute HTTP request
        var response = await _httpClient.PostAsync(_requestBuilder.GetApiUrl(), content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        // Validate and parse response using extracted service
        _responseParser.ValidateResponse(response, responseBody);
        return _responseParser.ParseResponse(responseBody);
    }

    /// <summary>
    /// Create AiResponse from parsed API response using tool execution service
    /// </summary>
    private static AiResponse CreateAiResponse(ResponsesApiResponse apiResponse)
    {
        var textContent = ExtractTextContent(apiResponse);
        var toolExecutions = ExtractToolExecutions(apiResponse);

        return new AiResponse(
            Content: textContent,
            ResponseId: apiResponse.Id ?? Guid.NewGuid().ToString(),
            ToolExecutions: toolExecutions);
    }

    /// <summary>
    /// Extract text content from API response (simplified version)
    /// </summary>
    private static string ExtractTextContent(ResponsesApiResponse response)
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
                    var jsonElement = (System.Text.Json.JsonElement)outputItem.Content;
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
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
                catch (InvalidOperationException)
                {
                    // Ignore parsing errors and continue
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extract tool executions from API response (simplified version for now)
    /// </summary>
    private static ToolExecution[]? ExtractToolExecutions(ResponsesApiResponse response)
    {
        if (response.Output == null || response.Output.Length == 0)
        {
            return null;
        }

        // For now, return null as we're focusing on text responses
        // Tool execution extraction can be enhanced later if needed
        return null;
    }
}