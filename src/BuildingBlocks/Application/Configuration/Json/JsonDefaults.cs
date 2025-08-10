using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Serialization.StrongIds;

namespace BuildingBlocks.Application.Configuration.Json;

/// <summary>
/// Provides default JsonSerializerOptions with StrongId support and common configuration.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Default JSON serialization options with StrongId support.
    /// Includes camelCase naming, null value ignoring, and enums as strings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // Add enum as string converter
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        
        // Add StrongId support
        options.Converters.Add(new StrongIdJsonConverterFactory());

        return options;
    }

    /// <summary>
    /// Creates a copy of the default options for customization.
    /// </summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(Options);
        return options;
    }

    /// <summary>
    /// Extension method to add StrongId support to existing options.
    /// </summary>
    public static JsonSerializerOptions AddStrongIdSupport(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StrongIdJsonConverterFactory());
        return options;
    }
}