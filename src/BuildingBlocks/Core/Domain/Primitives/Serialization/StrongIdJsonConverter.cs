using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// High-performance JSON converter for StrongId pattern (non-nullable).
/// Requires a public ctor (TPrimitive value).
/// </summary>
public sealed class StrongIdJsonConverter<TStrongId, TPrimitive> : JsonConverter<TStrongId>
    where TStrongId : struct, IStrongId<TPrimitive>
    where TPrimitive : struct, IEquatable<TPrimitive>
{
    private static readonly Func<TPrimitive, TStrongId> Ctor = CompileCtor();

    private static Func<TPrimitive, TStrongId> CompileCtor()
    {
        var ctor = typeof(TStrongId).GetConstructor(new[] { typeof(TPrimitive) });
        if (ctor == null)
            throw new InvalidOperationException(
                $"{typeof(TStrongId).Name} must have a public constructor {typeof(TStrongId).Name}({typeof(TPrimitive).Name} value).");

        var p = Expression.Parameter(typeof(TPrimitive), "v");
        var body = Expression.New(ctor, p);
        return Expression.Lambda<Func<TPrimitive, TStrongId>>(body, p).Compile();
    }

    public override TStrongId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Non-nullable: reject null
        if (reader.TokenType == JsonTokenType.Null)
            throw new JsonException($"{typeof(TStrongId).Name} cannot be null.");

        var primitive = JsonSerializer.Deserialize<TPrimitive>(ref reader, options);

        if (EqualityComparer<TPrimitive>.Default.Equals(primitive, default))
            throw new JsonException($"Cannot convert default value to {typeof(TStrongId).Name}.");

        return Ctor(primitive);
    }

    public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}