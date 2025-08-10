using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Application.Events.Serialization;

public sealed class SystemTextJsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            // Hook point: add StrongId/ValueObject converters here when available
            new JsonStringEnumConverter()
        }
    };

    public string Serialize(object message, Type messageType)
        => JsonSerializer.Serialize(message, messageType, Options);

    public object? Deserialize(string payload, Type messageType)
        => JsonSerializer.Deserialize(payload, messageType, Options);
}