using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Simple HTTP request builder implementation
/// </summary>
public sealed class HttpRequestBuilder : IHttpRequestBuilder
{
    private readonly OpenAiOptions _options;
    
    public HttpRequestBuilder(IOptions<OpenAiOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }
    
    public void ConfigureHttpClient(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }
    
    public HttpContent BuildRequestContent(AiRequest request, Activity? activity)
    {
        // Build payload according to OpenAI Responses API format
        var payload = new Dictionary<string, object>
        {
            ["model"] = _options.Model,
            ["input"] = request.Message
        };
        
        // Add optional parameters only if they have valid values
        if (_options.Temperature > 0)
        {
            payload["temperature"] = _options.Temperature;
        }
        
        if (_options.MaxTokens > 0)
        {
            payload["max_output_tokens"] = _options.MaxTokens; // Changed from max_tokens
        }
        
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
    
    public string GetApiUrl() => "https://api.openai.com/v1/responses";
}