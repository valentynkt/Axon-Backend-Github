using System.Text.Json.Serialization;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for chat conversations following SPARC IStrongId pattern.
/// Provides high-performance factory methods and JSON serialization support.
/// </summary>
[JsonConverter(typeof(StrongIdJsonConverter<ConversationId, Guid>))]
public readonly record struct ConversationId(Guid Value) : IStrongId<Guid>
{
    /// <summary>
    /// Creates a new unique ConversationId
    /// </summary>
    public static ConversationId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a ConversationId from a Guid value (IStrongId pattern)
    /// </summary>
    public static ConversationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ConversationId id) => id.Value;
}