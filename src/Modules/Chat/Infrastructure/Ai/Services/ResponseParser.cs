using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using Axon.Modules.Chat.Infrastructure.Ai.Models;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Simple response parser implementation
/// </summary>
public sealed class ResponseParser : IResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    public void ValidateResponse(HttpResponseMessage response, string responseBody)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API error: {response.StatusCode} - {responseBody}");
        }
    }
    
    public ResponsesApiResponse ParseResponse(string responseBody)
    {
        try
        {
            return JsonSerializer.Deserialize<ResponsesApiResponse>(responseBody, SerializerOptions) 
                ?? new ResponsesApiResponse(null, null);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse API response", ex);
        }
    }
}