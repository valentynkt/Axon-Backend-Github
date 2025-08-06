using System;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Core.Results;

namespace Axon.Modules.Chat.Domain.ValueObjects
{
    /// <summary>
    /// Strongly-typed identifier for a Message.
    /// </summary>
    [JsonConverter(typeof(StrongIdJsonConverterFactory))]
    public readonly record struct MessageId(Guid Value) : IStrongId<Guid>
    {
        public static MessageId New() => new(Guid.NewGuid());
        public static MessageId From(Guid value) => new(value);

        public static Result<MessageId> Create(Guid value)
        {
            if (value == Guid.Empty)
                return Error.Validation("MessageId cannot be empty.", "MESSAGE_ID_EMPTY");
            return new MessageId(value);
        }

        public override string ToString() => Value.ToString();
        public static implicit operator Guid(MessageId id) => id.Value;
        public static implicit operator MessageId(Guid value) => new(value);
    }
}