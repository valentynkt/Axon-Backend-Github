using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Simple JSON serialization service implementation
/// </summary>
public sealed class JsonSerializationService : IJsonSerializationService
{
    private readonly JsonSerializerOptions _options;
    
    public JsonSerializationService()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    public string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, _options);
    }
    
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}