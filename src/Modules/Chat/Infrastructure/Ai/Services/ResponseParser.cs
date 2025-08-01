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

        _logger.LogDebug(
            "Received response from OpenAI with ID {ResponseId} and {ContentLength} characters",
            apiResponse.Id,
            apiResponse.OutputText?.Length ?? 0);

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
            _logger.LogError(
                "OpenAI Responses API returned error {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody);
                
            throw new HttpRequestException(
                $"OpenAI API returned {response.StatusCode}: {responseBody}");
        }
    }
}