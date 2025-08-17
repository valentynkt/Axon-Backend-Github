using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Implementation of JSON serialization service maintaining consistent serialization
/// </summary>
public sealed class JsonSerializationService : IJsonSerializationService
{
    private static readonly JsonSerializerOptions _snakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions _standardOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string SerializeToSnakeCase(object obj)
    {
        return JsonSerializer.Serialize(obj, _snakeCaseOptions);
    }

    public T? DeserializeFromSnakeCase<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _snakeCaseOptions);
    }

    public string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, _standardOptions);
    }
}