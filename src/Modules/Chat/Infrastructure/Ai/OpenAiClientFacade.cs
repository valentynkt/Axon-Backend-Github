using System.Diagnostics;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Axon.Shared.Common;
using Microsoft.Extensions.Logging;
using InfraActivityTracker = Axon.Modules.Chat.Infrastructure.Ai.Abstractions.IActivityTracker;

namespace Axon.Modules.Chat.Infrastructure.Ai;

/// <summary>
/// OpenAI client facade with simplified interface and focused responsibilities
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class OpenAiClientFacade : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IResponseParser _responseParser;
    private readonly IToolExecutionExtractor _toolExtractor;
    private readonly InfraActivityTracker _activityTracker;
    private readonly ILogger<OpenAiClientFacade> _logger;

    public OpenAiClientFacade(
        HttpClient httpClient,
        IHttpRequestBuilder requestBuilder,
        IResponseParser responseParser,
        IToolExecutionExtractor toolExtractor,
        InfraActivityTracker activityTracker,
        ILogger<OpenAiClientFacade> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestBuilder);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(toolExtractor);
        ArgumentNullException.ThrowIfNull(activityTracker);
        
        _httpClient = httpClient;
        _requestBuilder = requestBuilder;
        _responseParser = responseParser;
        _toolExtractor = toolExtractor;
        _activityTracker = activityTracker;
        _logger = logger;
        
        // Configure HttpClient using the request builder
        _requestBuilder.ConfigureHttpClient(_httpClient);
    }

    public async Task<Result<AiResponse>> ProcessMessageAsync(
        AiRequest request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        using var activity = _activityTracker.StartActivity(request);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _activityTracker.LogProcessingStart("OpenAI Model", request.McpConfigs?.Count ?? 0);

            // Build and execute OpenAI Responses API request with Direct MCP
            var response = await ExecuteRequest(request, activity, cancellationToken);
            stopwatch.Stop();

            // Parse tool executions from the response
            var toolExecutions = _toolExtractor.ExtractToolExecutions(response, stopwatch.Elapsed, activity);

            // Extract text content from response
            var textContent = ExtractTextContent(response);
            
            var aiResponse = new AiResponse(
                Content: textContent,
                ResponseId: response.Id ?? Guid.NewGuid().ToString(),
                ToolExecutions: toolExecutions);

            _activityTracker.TagResponse(activity, aiResponse, stopwatch.Elapsed);
            _activityTracker.LogProcessingSuccess(stopwatch.Elapsed, toolExecutions?.Length ?? 0);

            return aiResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HandleProcessingException(ex, stopwatch, activity);
        }
    }

    /// <summary>
    /// Execute OpenAI Responses API request with Direct MCP integration
    /// </summary>
    private async Task<ResponsesApiResponse> ExecuteRequest(
        AiRequest request, 
        Activity? activity,
        CancellationToken cancellationToken)
    {
        // Build request content using the request builder
        using var content = _requestBuilder.BuildRequestContent(request, activity);
        
        // Execute request
        var response = await _httpClient.PostAsync(_requestBuilder.GetApiUrl(), content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        // Validate and parse response using the response parser
        _responseParser.ValidateResponse(response, responseBody);
        return _responseParser.ParseResponse(responseBody);
    }

    /// <summary>
    /// Handle exceptions that occur during message processing
    /// </summary>
    private Result<AiResponse> HandleProcessingException(Exception ex, Stopwatch stopwatch, Activity? activity)
    {
        stopwatch.Stop();
        _activityTracker.TagError(activity);
        _activityTracker.LogProcessingFailure(ex, stopwatch.Elapsed);

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
    /// Extract text content from OpenAI Responses API response
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
                catch (InvalidOperationException)
                {
                    // Ignore parsing errors and continue
                }
            }
        }

        return string.Empty;
    }
}