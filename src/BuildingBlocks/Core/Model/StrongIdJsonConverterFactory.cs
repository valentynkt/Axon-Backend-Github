using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Core.Model
{
    /// <summary>
    /// Factory that wires up both StrongIdJsonConverter<,> and NullableStrongIdJsonConverter<,>.
    /// </summary>
    public sealed class StrongIdJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            var t = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            if (!t.IsValueType) return false;
            return Array.Exists(
                t.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStrongId<>));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var underlying = Nullable.GetUnderlyingType(typeToConvert);
            var strongType = underlying ?? typeToConvert;
            var isNullable = underlying != null;

            var iface = Array.Find(
                            strongType.GetInterfaces(),
                            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStrongId<>))
                        ?? throw new InvalidOperationException($"{strongType} must implement IStrongId<T>.");

            var primitive = iface.GetGenericArguments()[0];
            var converterType = isNullable
                ? typeof(NullableStrongIdJsonConverter<,>)
                : typeof(StrongIdJsonConverter<,>);

            var closed = converterType.MakeGenericType(strongType, primitive);
            return (JsonConverter)Activator.CreateInstance(closed)!;
        }
    }
}