using System.Text.Json.Serialization;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for chat messages following SPARC IStrongId pattern.
/// Provides high-performance factory methods and JSON serialization support.
/// </summary>
[JsonConverter(typeof(StrongIdJsonConverter<MessageId, Guid>))]
public readonly record struct MessageId(Guid Value) : IStrongId<Guid>
{
    /// <summary>
    /// Creates a new unique MessageId
    /// </summary>
    public static MessageId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a MessageId from a Guid value (IStrongId pattern)
    /// </summary>
    public static MessageId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(MessageId id) => id.Value;
}