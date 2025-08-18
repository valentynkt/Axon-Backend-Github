namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// JSON serialization service
/// </summary>
public interface IJsonSerializationService
{
    /// <summary>
    /// Serialize object to JSON
    /// </summary>
    string Serialize(object obj);
    
    /// <summary>
    /// Deserialize JSON to object
    /// </summary>
    T? Deserialize<T>(string json);
}