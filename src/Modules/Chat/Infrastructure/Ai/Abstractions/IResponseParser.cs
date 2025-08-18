using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Parses OpenAI API responses
/// </summary>
public interface IResponseParser
{
    /// <summary>
    /// Validate the HTTP response
    /// </summary>
    void ValidateResponse(HttpResponseMessage response, string responseBody);
    
    /// <summary>
    /// Parse the response body
    /// </summary>
    ResponsesApiResponse ParseResponse(string responseBody);
}