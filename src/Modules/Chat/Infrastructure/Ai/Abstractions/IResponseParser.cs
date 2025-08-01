using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Service for parsing OpenAI API responses
/// </summary>
public interface IResponseParser
{
    /// <summary>
    /// Parses HTTP response into ResponsesApiResponse
    /// </summary>
    /// <param name="responseBody">Response body from OpenAI API</param>
    /// <returns>Parsed ResponsesApiResponse</returns>
    ResponsesApiResponse ParseResponse(string responseBody);

    /// <summary>
    /// Validates that the HTTP response was successful
    /// </summary>
    /// <param name="response">HTTP response message</param>
    /// <param name="responseBody">Response body content</param>
    /// <exception cref="HttpRequestException">Thrown when response indicates an error</exception>
    void ValidateResponse(HttpResponseMessage response, string responseBody);
}