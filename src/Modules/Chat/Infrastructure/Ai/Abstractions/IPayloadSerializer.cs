using System.Text.Json;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Service for JSON serialization operations
/// </summary>
public interface IPayloadSerializer
{
    /// <summary>
    /// Serializes an object to JSON string using snake_case naming
    /// </summary>
    /// <param name="value">Object to serialize</param>
    /// <returns>JSON string</returns>
    string Serialize(object value);

    /// <summary>
    /// Deserializes JSON string to specified type using snake_case naming
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized object</returns>
    T? Deserialize<T>(string json);

    /// <summary>
    /// Gets the JSON serializer options used by this service
    /// </summary>
    /// <returns>JsonSerializerOptions configured for snake_case</returns>
    JsonSerializerOptions GetOptions();
}