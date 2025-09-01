using System.Text.Json;

namespace Axon.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// JSON metadata storage with proper error handling and validation.
/// Encapsulates JSON serialization/deserialization operations.
/// </summary>
[ValueObject<string>(
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter | Conversions.EfCoreValueConverter)]
public readonly partial struct JsonMetadata
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static readonly JsonMetadata Empty = From("{}");

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Validation.Invalid("JSON metadata cannot be empty.");

        // Basic JSON validation by attempting to parse
        try
        {
            using var document = JsonDocument.Parse(input);
            
            // Check for reasonable size (32KB max)
            if (input.Length > 32768)
                return Validation.Invalid("JSON metadata cannot exceed 32KB.");
                
            return Validation.Ok;
        }
        catch (JsonException ex)
        {
            return Validation.Invalid($"Invalid JSON format: {ex.Message}");
        }
    }

    private static string NormalizeInput(string input) => input.Trim();

    /// <summary>
    /// Creates JsonMetadata from a dictionary.
    /// </summary>
    public static Result<JsonMetadata, Error> FromDictionary<T>(Dictionary<string, T>? data)
    {
        if (data is null || data.Count == 0)
            return Result.Success<JsonMetadata, Error>(Empty);

        try
        {
            var json = JsonSerializer.Serialize(data, SerializerOptions);
            var metadata = From(json);
            return Result.Success<JsonMetadata, Error>(metadata);
        }
        catch (JsonException ex)
        {
            return Result.Failure<JsonMetadata, Error>(
                Error.Internal($"Failed to serialize metadata: {ex.Message}", "IDENTITY.METADATA.SERIALIZATION.FAILED"));
        }
    }

    /// <summary>
    /// Deserializes to dictionary with error handling.
    /// </summary>
    public Result<Dictionary<string, T>, Error> ToDictionary<T>()
    {
        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, T>>(Value, SerializerOptions);
            return Result.Success<Dictionary<string, T>, Error>(dictionary ?? new Dictionary<string, T>());
        }
        catch (JsonException ex)
        {
            return Result.Failure<Dictionary<string, T>, Error>(
                Error.Internal($"Failed to deserialize metadata: {ex.Message}", "IDENTITY.METADATA.DESERIALIZATION.FAILED"));
        }
    }

    /// <summary>
    /// Deserializes to dictionary of objects with error handling.
    /// </summary>
    public Result<Dictionary<string, object>, Error> ToDictionary()
    {
        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(Value, SerializerOptions);
            return Result.Success<Dictionary<string, object>, Error>(dictionary ?? new Dictionary<string, object>());
        }
        catch (JsonException ex)
        {
            return Result.Failure<Dictionary<string, object>, Error>(
                Error.Internal($"Failed to deserialize metadata: {ex.Message}", "IDENTITY.METADATA.DESERIALIZATION.FAILED"));
        }
    }

    /// <summary>
    /// Gets a value from the JSON by key with error handling.
    /// </summary>
    public Result<T?, Error> GetValue<T>(string key)
    {
        var dictionaryResult = ToDictionary<T>();
        if (dictionaryResult.IsFailure)
            return Result.Failure<T?, Error>(dictionaryResult.Error);

        var dictionary = dictionaryResult.Value;
        var value = dictionary.GetValueOrDefault(key);
        return Result.Success<T?, Error>(value);
    }

    /// <summary>
    /// Checks if the JSON contains a key.
    /// </summary>
    public Result<bool, Error> ContainsKey(string key)
    {
        var dictionaryResult = ToDictionary<object>();
        if (dictionaryResult.IsFailure)
            return Result.Failure<bool, Error>(dictionaryResult.Error);

        var contains = dictionaryResult.Value.ContainsKey(key);
        return Result.Success<bool, Error>(contains);
    }

    public static Result<JsonMetadata, Error> Create(string? value)
    {
        if (value is null)
            return Result.Success<JsonMetadata, Error>(Empty);

        try
        {
            var vo = From(value);
            return Result.Success<JsonMetadata, Error>(vo);
        }
        catch (ValueObjectValidationException ex)
        {
            return Result.Failure<JsonMetadata, Error>(
                Error.Validation(ex.Message, "IDENTITY.METADATA.INVALID"));
        }
    }
}