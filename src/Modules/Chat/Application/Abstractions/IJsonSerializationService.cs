namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for JSON serialization operations to maintain consistent serialization across layers
/// </summary>
public interface IJsonSerializationService
{
    /// <summary>
    /// Serializes an object to JSON string using snake_case naming policy
    /// </summary>
    string SerializeToSnakeCase(object obj);
    
    /// <summary>
    /// Deserializes JSON string to specified type using snake_case naming policy
    /// </summary>
    T? DeserializeFromSnakeCase<T>(string json);
    
    /// <summary>
    /// Serializes an object to JSON string using standard naming policy
    /// </summary>
    string Serialize(object obj);
}