using System.Net;
using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Service responsible for parsing OpenAI API responses
/// </summary>
public sealed class ResponseParser : IResponseParser
{
    private readonly IPayloadSerializer _payloadSerializer;
    private readonly ILogger<ResponseParser> _logger;
    
    // LoggerMessage delegates for CA1848 compliance
    private static readonly Action<ILogger, string, int, Exception?> LogResponseReceivedAction =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(5001, "LogResponseReceived"),
            "Received response from OpenAI with ID {ResponseId} and {ContentLength} characters");
            
    private static readonly Action<ILogger, HttpStatusCode, string, Exception?> LogApiErrorAction =
        LoggerMessage.Define<HttpStatusCode, string>(
            LogLevel.Error,
            new EventId(5002, "LogApiError"),
            "OpenAI Responses API returned error {StatusCode}. Response: {ResponseBody}");

    public ResponseParser(
        IPayloadSerializer payloadSerializer,
        ILogger<ResponseParser> logger)
    {
        _payloadSerializer = payloadSerializer;
        _logger = logger;
    }

    /// <summary>
    /// Parses HTTP response into ResponsesApiResponse
    /// </summary>
    /// <param name="responseBody">Response body from OpenAI API</param>
    /// <returns>Parsed ResponsesApiResponse</returns>
    public ResponsesApiResponse ParseResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new JsonException("Response body is null or empty");
        }

        // Parse response using the serializer
        var apiResponse = _payloadSerializer.Deserialize<ResponsesApiResponse>(responseBody);
        
        if (apiResponse == null)
        {
            throw new JsonException("Failed to deserialize OpenAI Responses API response");
        }

        // Extract text content for logging
        var textContent = ExtractTextContent(apiResponse);
        LogResponseReceivedAction(_logger, apiResponse.Id ?? "unknown", textContent.Length, null);

        return apiResponse;
    }

    /// <summary>
    /// Validates that the HTTP response was successful
    /// </summary>
    /// <param name="response">HTTP response message</param>
    /// <param name="responseBody">Response body content</param>
    /// <exception cref="HttpRequestException">Thrown when response indicates an error</exception>
    public void ValidateResponse(HttpResponseMessage response, string responseBody)
    {
        if (!response.IsSuccessStatusCode)
        {
            LogApiErrorAction(_logger, response.StatusCode, responseBody, null);
                
            throw new HttpRequestException(
                $"OpenAI API returned {response.StatusCode}: {responseBody}");
        }
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