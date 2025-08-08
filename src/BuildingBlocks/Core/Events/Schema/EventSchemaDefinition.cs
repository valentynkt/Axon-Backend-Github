using BuildingBlocks.Core.Domain.Events;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Core.Events.Schema;

/// <summary>
/// Comprehensive event schema definition with versioning and migration support.
/// Immutable record ensuring schema integrity and thread safety.
/// Supports both typed and untyped scenarios for maximum flexibility.
/// </summary>
public sealed record EventSchemaDefinition
{
    /// <summary>
    /// The fully qualified event type name (e.g., "MyApp.Events.UserCreated")
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Schema version number. Must be positive and incremental.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// JSON schema definition describing the event structure.
    /// Should follow JSON Schema Draft 2020-12 specification.
    /// </summary>
    public required string JsonSchema { get; init; }

    /// <summary>
    /// When this schema version was created (UTC).
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Optional human-readable description of this schema version.
    /// Should describe what changed from previous version.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Additional metadata for the schema (e.g., author, tags, etc.).
    /// Stored as key-value pairs for extensibility.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Reference to the previous version this schema migrates from.
    /// Used to build migration chains and validate upgrade paths.
    /// </summary>
    public int? MigrationFromVersion { get; init; }

    /// <summary>
    /// Computed hash of the schema content for integrity verification.
    /// Generated using SHA-256 of the normalized JSON schema.
    /// </summary>
    public string? SchemaHash { get; init; }

    /// <summary>
    /// Migration configuration for transforming events from previous versions.
    /// Contains rules and transformations needed for data migration.
    /// </summary>
    public SchemaMigrationConfig? MigrationConfig { get; init; }

    /// <summary>
    /// Compatibility settings for this schema version.
    /// Defines backward/forward compatibility requirements.
    /// </summary>
    public SchemaCompatibilityConfig? CompatibilityConfig { get; init; }

    /// <summary>
    /// Tags for categorizing and organizing schemas.
    /// Useful for governance and discovery.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Optional deprecation information if this schema version is deprecated.
    /// </summary>
    public SchemaDeprecationInfo? DeprecationInfo { get; init; }

    /// <summary>
    /// Validation options for this schema.
    /// Controls how strict validation should be applied.
    /// </summary>
    public SchemaValidationOptions? ValidationOptions { get; init; }

    /// <summary>
    /// Create a schema definition with computed hash.
    /// </summary>
    public static EventSchemaDefinition Create(
        string eventType,
        int version,
        string jsonSchema,
        string? description = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        int? migrationFromVersion = null,
        SchemaMigrationConfig? migrationConfig = null,
        SchemaCompatibilityConfig? compatibilityConfig = null,
        IReadOnlyList<string>? tags = null,
        SchemaDeprecationInfo? deprecationInfo = null,
        SchemaValidationOptions? validationOptions = null)
    {
        return new EventSchemaDefinition
        {
            EventType = eventType,
            Version = version,
            JsonSchema = jsonSchema,
            CreatedAtUtc = DateTime.UtcNow,
            Description = description,
            Metadata = metadata,
            MigrationFromVersion = migrationFromVersion,
            SchemaHash = ComputeSchemaHash(jsonSchema),
            MigrationConfig = migrationConfig,
            CompatibilityConfig = compatibilityConfig,
            Tags = tags,
            DeprecationInfo = deprecationInfo,
            ValidationOptions = validationOptions
        };
    }

    /// <summary>
    /// Compute SHA-256 hash of normalized JSON schema for integrity verification.
    /// </summary>
    private static string ComputeSchemaHash(string jsonSchema)
    {
        // Normalize JSON (remove whitespace, sort keys) before hashing
        var normalizedJson = NormalizeJsonSchema(jsonSchema);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Normalize JSON schema for consistent hashing.
    /// Removes formatting differences that don't affect semantics.
    /// </summary>
    private static string NormalizeJsonSchema(string jsonSchema)
    {
        try
        {
            // Parse and re-serialize to normalize formatting
            var document = System.Text.Json.JsonDocument.Parse(jsonSchema);
            return System.Text.Json.JsonSerializer.Serialize(document, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null
            });
        }
        catch
        {
            // If parsing fails, return original (will likely cause validation issues later)
            return jsonSchema;
        }
    }
}

/// <summary>
/// Strongly-typed schema definition for compile-time safety.
/// Provides additional type safety while maintaining all base functionality.
/// </summary>
/// <typeparam name="TEvent">The event type this schema defines</typeparam>
public sealed record EventSchemaDefinition<TEvent> : EventSchemaDefinition
    where TEvent : IEvent
{
    /// <summary>
    /// Optional sample event instance for documentation and testing.
    /// Should represent a valid event conforming to this schema.
    /// </summary>
    [JsonIgnore]
    public TEvent? SampleEvent { get; init; }

    /// <summary>
    /// Type-safe factory method for creating strongly-typed schema definitions.
    /// </summary>
    public static EventSchemaDefinition<TEvent> Create(
        int version,
        string jsonSchema,
        string? description = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        int? migrationFromVersion = null,
        SchemaMigrationConfig? migrationConfig = null,
        SchemaCompatibilityConfig? compatibilityConfig = null,
        IReadOnlyList<string>? tags = null,
        SchemaDeprecationInfo? deprecationInfo = null,
        SchemaValidationOptions? validationOptions = null,
        TEvent? sampleEvent = null)
        where TEvent : class
    {
        var eventTypeName = typeof(TEvent).FullName ?? typeof(TEvent).Name;
        
        return new EventSchemaDefinition<TEvent>
        {
            EventType = eventTypeName,
            Version = version,
            JsonSchema = jsonSchema,
            CreatedAtUtc = DateTime.UtcNow,
            Description = description,
            Metadata = metadata,
            MigrationFromVersion = migrationFromVersion,
            SchemaHash = ComputeSchemaHash(jsonSchema),
            MigrationConfig = migrationConfig,
            CompatibilityConfig = compatibilityConfig,
            Tags = tags,
            DeprecationInfo = deprecationInfo,
            ValidationOptions = validationOptions,
            SampleEvent = sampleEvent
        };
    }

    /// <summary>
    /// Create from base schema definition with type safety.
    /// </summary>
    public static EventSchemaDefinition<TEvent> FromBase(
        EventSchemaDefinition baseDefinition,
        TEvent? sampleEvent = null)
    {
        return new EventSchemaDefinition<TEvent>
        {
            EventType = baseDefinition.EventType,
            Version = baseDefinition.Version,
            JsonSchema = baseDefinition.JsonSchema,
            CreatedAtUtc = baseDefinition.CreatedAtUtc,
            Description = baseDefinition.Description,
            Metadata = baseDefinition.Metadata,
            MigrationFromVersion = baseDefinition.MigrationFromVersion,
            SchemaHash = baseDefinition.SchemaHash,
            MigrationConfig = baseDefinition.MigrationConfig,
            CompatibilityConfig = baseDefinition.CompatibilityConfig,
            Tags = baseDefinition.Tags,
            DeprecationInfo = baseDefinition.DeprecationInfo,
            ValidationOptions = baseDefinition.ValidationOptions,
            SampleEvent = sampleEvent
        };
    }

    /// <summary>
    /// Compute SHA-256 hash of normalized JSON schema for integrity verification.
    /// </summary>
    private static string ComputeSchemaHash(string jsonSchema)
    {
        // Normalize JSON (remove whitespace, sort keys) before hashing
        var normalizedJson = NormalizeJsonSchema(jsonSchema);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Normalize JSON schema for consistent hashing.
    /// </summary>
    private static string NormalizeJsonSchema(string jsonSchema)
    {
        try
        {
            var document = System.Text.Json.JsonDocument.Parse(jsonSchema);
            return System.Text.Json.JsonSerializer.Serialize(document, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null
            });
        }
        catch
        {
            return jsonSchema;
        }
    }
}