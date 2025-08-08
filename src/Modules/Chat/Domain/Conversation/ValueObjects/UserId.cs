using System.Text.Json.Serialization;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Primitives.Serialization;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects
{
    /// <summary>
    /// Strongly-typed identifier for a User.
    /// </summary>
    [JsonConverter(typeof(StrongIdJsonConverterFactory))]
    public readonly record struct UserId(Guid Value) : IStrongId<Guid>
    {
        public static UserId New() => new(Guid.NewGuid());
        public static UserId From(Guid value) => new(value);

        public static Result<UserId> Create(Guid value)
        {
            if (value == Guid.Empty)
                return Error.Validation("UserId cannot be empty.", "USER_ID_EMPTY");
            return new UserId(value);
        }

        public override string ToString() => Value.ToString();
        public static implicit operator Guid(UserId id) => id.Value;
        public static implicit operator UserId(Guid value) => new(value);
    }
}