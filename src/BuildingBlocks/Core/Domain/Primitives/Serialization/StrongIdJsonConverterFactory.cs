using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Core.Domain.Primitives.Serialization;

/// <summary>
/// Robust JSON converter factory for strongly-typed identifiers.
/// Handles null values, complex constructors, and provides comprehensive error handling.
/// </summary>
public class StrongIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return IsStrongIdType(typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = GetStrongIdValueType(typeToConvert);
        var converterType = typeof(StrongIdJsonConverter<,>).MakeGenericType(typeToConvert, valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static bool IsStrongIdType(Type type)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static Type GetStrongIdValueType(Type strongIdType)
    {
        var current = strongIdType;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
                return current.GetGenericArguments()[0];
            current = current.BaseType;
        }
        throw new ArgumentException($"Type {strongIdType} is not a StrongId");
    }
}

public class StrongIdJsonConverter<TStrongId, TValue> : JsonConverter<TStrongId>
    where TStrongId : StrongId<TValue>
    where TValue : struct, IComparable<TValue>, IEquatable<TValue>
{
    private static readonly ConcurrentDictionary<Type, Func<TValue, TStrongId>> _factoryCache = new();
    
    public override TStrongId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle null values gracefully
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException($"Cannot deserialize null value to StrongId type {typeToConvert.Name}");
        }
        
        try
        {
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            return CreateInstance(typeToConvert, value);
        }
        catch (JsonException)
        {
            throw; // Re-throw JSON exceptions
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize {typeToConvert.Name}: {ex.Message}", ex);
        }
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

    /// <summary>
    /// Creates StrongId instance using cached factory with robust constructor resolution
    /// </summary>
    private static TStrongId CreateInstance(Type strongIdType, TValue value)
    {
        var factory = _factoryCache.GetOrAdd(strongIdType, type =>
        {
            // Try multiple constructor resolution strategies
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(c => c.GetParameters().Length == 1)
                .Where(c => c.GetParameters()[0].ParameterType == typeof(TValue))
                .OrderBy(c => c.IsPublic ? 0 : 1) // Prefer public constructors
                .ToArray();

            if (constructors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for {type.Name} that accepts {typeof(TValue).Name}");
            }

            var constructor = constructors[0];
            
            // Create compiled factory for performance
            var parameter = Expression.Parameter(typeof(TValue), "value");
            var newExpression = Expression.New(constructor, parameter);
            var lambda = Expression.Lambda<Func<TValue, TStrongId>>(newExpression, parameter);
            
            return lambda.Compile();
        });

        return factory(value);
    }
}

/// <summary>
/// Extension methods for StrongId JSON configuration
/// </summary>
public static class StrongIdJsonExtensions
{
    /// <summary>
    /// Configure JSON options to use StrongId converters
    /// </summary>
    public static JsonSerializerOptions AddStrongIdSupport(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StrongIdJsonConverterFactory());
        return options;
    }
}