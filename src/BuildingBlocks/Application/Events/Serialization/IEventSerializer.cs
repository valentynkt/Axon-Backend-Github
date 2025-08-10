namespace BuildingBlocks.Application.Events.Serialization;

public interface IEventSerializer
{
    string Serialize(object message, Type messageType);
    object? Deserialize(string payload, Type messageType);

    // Convenience generics
    string Serialize<T>(T message) where T : notnull
        => Serialize(message, typeof(T));

    T? Deserialize<T>(string payload)
        => (T?)Deserialize(payload, typeof(T));
}