using System.Diagnostics;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Service for building HTTP requests and payloads for OpenAI API
/// </summary>
public interface IHttpRequestBuilder
{
    /// <summary>
    /// Configures HttpClient with OpenAI authentication and headers
    /// </summary>
    /// <param name="httpClient">HttpClient to configure</param>
    void ConfigureHttpClient(HttpClient httpClient);

    /// <summary>
    /// Builds HTTP content for OpenAI Responses API request
    /// </summary>
    /// <param name="request">AI request</param>
    /// <param name="activity">Activity for tracing</param>
    /// <returns>HTTP content ready for sending</returns>
    StringContent BuildRequestContent(AiRequest request, Activity? activity);

    /// <summary>
    /// Gets the OpenAI API URL
    /// </summary>
    /// <returns>API URL</returns>
    string GetApiUrl();
}