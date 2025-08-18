namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Serializes payloads for API requests
/// </summary>
public interface IPayloadSerializer
{
    /// <summary>
    /// Serialize object to JSON
    /// </summary>
    string Serialize(object payload);
}