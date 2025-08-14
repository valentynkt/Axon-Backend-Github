using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;

namespace BuildingBlocks.Infrastructure.Messaging.Serialization;

/// <summary>
/// Centralized System.Text.Json configuration for MassTransit.
/// Ensures consistent serialization across the messaging infrastructure.
/// </summary>
public static class SystemTextJsonConfigurator
{
    /// <summary>
    /// Creates JsonSerializerOptions configured for MassTransit messaging.
    /// </summary>
    public static JsonSerializerOptions CreateOptions(SerializationOptions? config = null)
    {
        config ??= new SerializationOptions();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = config.UseCamelCase ? JsonNamingPolicy.CamelCase : null,
            DefaultIgnoreCondition = config.IgnoreNullValues 
                ? JsonIgnoreCondition.WhenWritingNull 
                : JsonIgnoreCondition.Never,
            WriteIndented = false,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        // Add converters
        if (config.EnumsAsStrings)
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        // Add ISO 8601 date handling (System.Text.Json handles this by default)
        // But we can add custom date converters if needed
        if (config.UseIso8601Dates)
        {
            options.Converters.Add(new Iso8601DateTimeOffsetConverter());
            options.Converters.Add(new Iso8601DateTimeConverter());
        }

        // Add StrongId converters if they exist in the assembly
        TryAddStrongIdConverters(options);

        return options;
    }

    /// <summary>
    /// Configures System.Text.Json for use with MassTransit.
    /// </summary>
    public static void Configure(IBusFactoryConfigurator configurator, SerializationOptions? config = null)
    {
        var options = CreateOptions(config);
        configurator.ConfigureJsonSerializerOptions(opts => options);
    }

    private static void TryAddStrongIdConverters(JsonSerializerOptions options)
    {
        // Try to find and add StrongId converters dynamically
        // This assumes StrongId<T> follows a pattern we can detect
        var strongIdConverterType = Type.GetType("BuildingBlocks.Core.Serialization.StrongIdJsonConverter, BuildingBlocks");
        if (strongIdConverterType != null)
        {
            try
            {
                var converter = Activator.CreateInstance(strongIdConverterType) as JsonConverter;
                if (converter != null)
                {
                    options.Converters.Add(converter);
                }
            }
            catch
            {
                // Silently ignore if we can't create the converter
            }
        }
    }

    /// <summary>
    /// Custom converter for ISO 8601 DateTimeOffset values.
    /// </summary>
    private class Iso8601DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O")); // ISO 8601 format
        }
    }

    /// <summary>
    /// Custom converter for ISO 8601 DateTime values.
    /// </summary>
    private class Iso8601DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O")); // ISO 8601 format
        }
    }
}