using System.Text.Json;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Service responsible for JSON serialization operations
/// </summary>
public sealed class PayloadSerializer : IPayloadSerializer
{
    private static readonly JsonSerializerOptions _snakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes an object to JSON string using snake_case naming
    /// </summary>
    /// <param name="value">Object to serialize</param>
    /// <returns>JSON string</returns>
    public string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, _snakeCaseJsonOptions);
    }

    /// <summary>
    /// Deserializes JSON string to specified type using snake_case naming
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized object</returns>
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _snakeCaseJsonOptions);
    }

    /// <summary>
    /// Gets the JSON serializer options used by this service
    /// </summary>
    /// <returns>JsonSerializerOptions configured for snake_case</returns>
    public JsonSerializerOptions GetOptions() => _snakeCaseJsonOptions;
}