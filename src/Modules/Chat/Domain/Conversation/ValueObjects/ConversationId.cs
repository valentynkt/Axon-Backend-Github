using System.Text.Json.Serialization;
using Axon.Shared.Common;
using BuildingBlocks.Core.Domain.Primitives.Serialization;
using BuildingBlocks.Core.Functional.Results;

// StrongIdJsonConverter (if defined here) or your shared converter

namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Conversation.
/// Thin wrapper over Guid with value semantics and JSON support.
/// </summary>
[JsonConverter(typeof(StrongIdJsonConverterFactory))]
public readonly record struct ConversationId(Guid Value) : global::BuildingBlocks.Core.Model.IStrongId<Guid>
{
    /// <summary>Create a new unique ConversationId.</summary>
    public static ConversationId New() => new(Guid.NewGuid());

    /// <summary>Wrap an existing Guid (no validation).</summary>
    public static ConversationId From(Guid value) => new(value);

    /// <summary>Validate and create (prefers explicit Result flow).</summary>
    public static Result<ConversationId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Error.Validation("ConversationId cannot be empty.", "CONVERSATION_ID_EMPTY");

        return new ConversationId(value);
    }

    public override string ToString() => Value.ToString();

    // Convenience conversions
    public static implicit operator Guid(ConversationId id) => id.Value;
    public static explicit operator ConversationId(Guid value) => new(value);
}