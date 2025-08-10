using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Serialization.StrongIds;

/// <summary>
/// JSON converter factory for StrongId&lt;TPrimitive&gt; (record-class) identifiers.
/// - Detects any type whose base is StrongId&lt;TPrimitive&gt;
/// - Compiles a fast ctor(TPrimitive) for deserialization
/// - Handles null tokens and guards against default(TPrimitive)
/// - Supports Guid, int, long, string primitives (extensible)
/// </summary>
public sealed class StrongIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => TryGetStrongIdPrimitive(typeToConvert, out _);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // If called for nullable reference sites (TStrongId?), CLR type is still TStrongId.
        if (!TryGetStrongIdPrimitive(typeToConvert, out var primitiveType))
            throw new InvalidOperationException($"Type {typeToConvert} is not a StrongId<>.");

        var converterType = typeof(StrongIdConverter<,>).MakeGenericType(typeToConvert, primitiveType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static bool TryGetStrongIdPrimitive(Type candidate, out Type primitiveType)
    {
        // Walk inheritance chain to find StrongId<TPrimitive>
        var current = candidate;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
            {
                primitiveType = current.GetGenericArguments()[0];
                return IsSupportedPrimitive(primitiveType);
            }
            current = current.BaseType!;
        }

        primitiveType = null!;
        return false;
    }

    private static bool IsSupportedPrimitive(Type primitiveType)
        => primitiveType == typeof(Guid) ||
           primitiveType == typeof(int) ||
           primitiveType == typeof(long) ||
           primitiveType == typeof(string);

    /// <summary>
    /// Non-nullable converter for StrongId-derived reference types.
    /// Also handles null JSON tokens gracefully (returns null).
    /// </summary>
    private sealed class StrongIdConverter<TStrongId, TPrimitive> : JsonConverter<TStrongId>
        where TStrongId : StrongId<TPrimitive>
        where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
    {
        private static readonly Func<TPrimitive, TStrongId> _factory = CompileFactory();

        public override TStrongId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null; // reference type: allow nulls

            // Deserialize underlying primitive
            var value = JsonSerializer.Deserialize<TPrimitive>(ref reader, options);

            // Guard against default(TPrimitive) (StrongId base would throw anyway)
            if (EqualityComparer<TPrimitive>.Default.Equals(value, default))
                throw new JsonException($"Cannot deserialize default({typeof(TPrimitive).Name}) to {typeof(TStrongId).Name}.");

            return _factory(value);
        }

        public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, value.Value, options);
        }

        private static Func<TPrimitive, TStrongId> CompileFactory()
        {
            // Prefer public ctor(TPrimitive), fallback to non-public if necessary
            var ctor = typeof(TStrongId)
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c =>
                {
                    var p = c.GetParameters();
                    return p.Length == 1 && p[0].ParameterType == typeof(TPrimitive);
                });

            if (ctor is null)
                throw new InvalidOperationException(
                    $"{typeof(TStrongId).Name} must expose a constructor accepting ({typeof(TPrimitive).Name} value).");

            var param = Expression.Parameter(typeof(TPrimitive), "v");
            var body  = Expression.New(ctor, param);
            return Expression.Lambda<Func<TPrimitive, TStrongId>>(body, param).Compile();
        }
    }
}