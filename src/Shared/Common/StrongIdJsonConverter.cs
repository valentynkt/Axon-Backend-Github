using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace Axon.Shared.Common;

/// <summary>
/// High-performance JSON converter for StrongId pattern with optimized serialization.
/// Uses optimized instantiation to minimize reflection overhead.
/// Follows SPARC architecture requirements for performant JSON serialization.
/// </summary>
/// <typeparam name="TStrongId">The strongly-typed identifier type</typeparam>
/// <typeparam name="TValue">The underlying value type</typeparam>
public sealed class StrongIdJsonConverter<TStrongId, TValue> : JsonConverter<TStrongId>
    where TStrongId : struct, IStrongId<TValue>
    where TValue : struct, IEquatable<TValue>
{
    // Cache the constructor for performance
    private static readonly Func<TValue, TStrongId>? _constructor = CreateConstructor();

    private static Func<TValue, TStrongId>? CreateConstructor()
    {
        var constructorInfo = typeof(TStrongId).GetConstructor(new[] { typeof(TValue) });
        if (constructorInfo == null) return null;

        return value => (TStrongId)constructorInfo.Invoke(new object[] { value! });
    }

    public override TStrongId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        
        // Guard against null/default values for robustness
        if (EqualityComparer<TValue>.Default.Equals(value, default))
        {
            throw new JsonException($"Cannot convert null or default value to {typeof(TStrongId).Name}");
        }
        
        // Use cached constructor for optimal performance
        if (_constructor != null)
        {
            return _constructor(value);
        }
        
        throw new JsonException($"Cannot create instance of {typeof(TStrongId).Name}: constructor not found");
    }

    public override void Write(Utf8JsonWriter writer, TStrongId value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}