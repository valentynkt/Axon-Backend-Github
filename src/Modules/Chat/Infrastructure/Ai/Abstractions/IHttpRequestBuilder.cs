using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Builds HTTP requests for OpenAI API
/// </summary>
public interface IHttpRequestBuilder
{
    /// <summary>
    /// Configure the HTTP client
    /// </summary>
    void ConfigureHttpClient(HttpClient httpClient);
    
    /// <summary>
    /// Build request content
    /// </summary>
    HttpContent BuildRequestContent(AiRequest request, Activity? activity);
    
    /// <summary>
    /// Get the API URL
    /// </summary>
    string GetApiUrl();
}