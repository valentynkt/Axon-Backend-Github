using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// JSON converter for Nullable&lt;TStrongId&gt;.
/// </summary>
public sealed class NullableStrongIdJsonConverter<TStrongId, TPrimitive> : JsonConverter<TStrongId?>
    where TStrongId : struct, IStrongId<TPrimitive>
    where TPrimitive : struct, IEquatable<TPrimitive>
{
    private static readonly StrongIdJsonConverter<TStrongId, TPrimitive> Inner = new();

    public override TStrongId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var primitive = JsonSerializer.Deserialize<TPrimitive>(ref reader, options);
        if (EqualityComparer<TPrimitive>.Default.Equals(primitive, default))
            return null; // nullable: treat default as null

        var json = JsonSerializer.Serialize(primitive, options);
        var r = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        r.Read();
        return Inner.Read(ref r, typeof(TStrongId), options);
    }

    public override void Write(Utf8JsonWriter writer, TStrongId? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value.Value.Value, options);
    }
}