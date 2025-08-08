# Story 03: Event Schema Registry & Versioning

## Story Overview

**Story ID**: Epic_06_Story_03  
**Story Name**: Event Schema Registry & Versioning  
**Epic**: Epic 06 - Event System Enhancement & Integration  
**Priority**: P1 - High  
**Estimated Duration**: 6 hours  
**Dependencies**: Epic_06_Story_01 (Outbox Processor), Epic_06_Story_02 (Integration Publisher)

## User Story

**As a developer**, I want event schema evolution support so that I can safely version and migrate events without breaking existing consumers, enabling continuous deployment and backward compatibility.

## Current State Analysis

### ✅ What Exists Today
- **Event Hierarchy**: Well-defined `DomainEvent`, `IntegrationEventBase` with version fields
- **Serialization**: `SystemTextJsonEventSerializer` with event type handling
- **Event Mapping**: `CompositeEventMapper` for domain→integration transformations
- **Version Field**: Events contain `Version` property (int) for basic versioning

### ❌ What's Missing
- **Schema Registry**: No centralized schema management or validation
- **Migration Logic**: No automatic event version migration capabilities
- **Compatibility Validation**: No backward compatibility checks before deployment
- **Schema Documentation**: No discoverable schema definitions for consumers
- **Multiple Formats**: Only JSON supported, no Avro/Protobuf options

### 🔍 Current Event Version Usage
```csharp
// Events have version field but no migration logic
public abstract record DomainEvent : IDomainEvent
{
    public int Version { get; init; } = 1; // Static version, no migration
}

public abstract record IntegrationEventBase : EventBase, IIntegrationEvent
{
    // Same static versioning approach
}
```

## Acceptance Criteria

### Schema Management Requirements
- [ ] `IEventSchemaRegistry` provides centralized schema storage and retrieval
- [ ] Event schemas automatically registered on application startup
- [ ] Schema evolution rules prevent breaking changes without explicit migration
- [ ] Schema documentation generated and discoverable by external consumers

### Version Migration Requirements
- [ ] Automatic event deserialization with version migration pipeline
- [ ] Support for multi-step migrations (v1→v2→v3) through migration chains
- [ ] Migration validation during application startup (fail fast on invalid migrations)
- [ ] Rollback capability for failed migrations

### Compatibility Requirements
- [ ] Backward compatibility validation before schema updates
- [ ] Forward compatibility support for newer consumers processing older events
- [ ] Breaking change detection with developer-friendly error messages
- [ ] Contract testing framework for event schema compatibility

### Operational Requirements
- [ ] Schema registry health checks and monitoring
- [ ] Migration performance metrics and logging
- [ ] Schema diff visualization for code reviews
- [ ] Configuration-driven schema validation strictness levels

## Technical Implementation

### 1. Event Schema Registry Interface

**Location**: `src/BuildingBlocks/Core/Abstractions/Events/IEventSchemaRegistry.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Registry for managing event schemas, versioning, and migration logic
/// Provides centralized schema storage with backward compatibility validation
/// </summary>
public interface IEventSchemaRegistry
{
    /// <summary>
    /// Register event schema with version and migration information
    /// </summary>
    Task<Result<Unit>> RegisterSchemaAsync<TEvent>(
        EventSchemaDefinition<TEvent> schemaDefinition,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Get current schema definition for event type
    /// </summary>
    Task<Result<EventSchemaDefinition>> GetSchemaAsync(
        string eventType,
        int? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions of an event schema
    /// </summary>
    Task<Result<IReadOnlyList<EventSchemaDefinition>>> GetSchemaVersionsAsync(
        string eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate schema compatibility between versions
    /// </summary>
    Task<Result<CompatibilityResult>> ValidateCompatibilityAsync(
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrate event from one version to another
    /// </summary>
    Task<Result<object>> MigrateEventAsync(
        object eventData,
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get schema registry health status
    /// </summary>
    Task<Result<SchemaRegistryHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Definition of an event schema including version and migration information
/// </summary>
public sealed record EventSchemaDefinition
{
    public required string EventType { get; init; }
    public required int Version { get; init; }
    public required string JsonSchema { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    public string? MigrationFromVersion { get; init; }
    public string? SchemaHash { get; init; }
}

/// <summary>
/// Strongly typed schema definition with migration logic
/// </summary>
public sealed record EventSchemaDefinition<TEvent> : EventSchemaDefinition
    where TEvent : IEvent
{
    public required Type EventClrType { get; init; }
    public Func<object, int, int, Task<Result<TEvent>>>? MigrationFunc { get; init; }
}

/// <summary>
/// Result of schema compatibility validation
/// </summary>
public sealed record CompatibilityResult
{
    public bool IsCompatible { get; init; }
    public CompatibilityLevel Level { get; init; }
    public IReadOnlyList<CompatibilityIssue> Issues { get; init; } = Array.Empty<CompatibilityIssue>();
    public IReadOnlyList<string> MigrationSteps { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Compatibility levels between schema versions
/// </summary>
public enum CompatibilityLevel
{
    Full,           // Fully compatible in both directions
    Backward,       // New version can read old events
    Forward,        // Old version can read new events  
    Breaking        // Incompatible - requires migration
}

/// <summary>
/// Details of compatibility issues found
/// </summary>
public sealed record CompatibilityIssue
{
    public required string FieldPath { get; init; }
    public required IssueType Type { get; init; }
    public required string Description { get; init; }
    public string? Suggestion { get; init; }
}

public enum IssueType
{
    FieldRemoved,
    FieldTypeChanged,
    RequiredFieldAdded,
    EnumValueChanged,
    ArrayTypeChanged
}

/// <summary>
/// Health status of schema registry
/// </summary>
public sealed record SchemaRegistryHealth
{
    public bool IsHealthy { get; init; }
    public int RegisteredSchemasCount { get; init; }
    public int MigrationsCount { get; init; }
    public DateTime LastRegistrationUtc { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### 2. Database Schema Registry Implementation

**Location**: `src/BuildingBlocks/Infrastructure/Events/DatabaseEventSchemaRegistry.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Database-backed implementation of event schema registry
/// Stores schemas in dedicated table with versioning and migration support
/// </summary>
public sealed class DatabaseEventSchemaRegistry : IEventSchemaRegistry
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<EventSchemaOptions> _options;
    private readonly ILogger<DatabaseEventSchemaRegistry> _logger;
    private readonly ConcurrentDictionary<string, EventSchemaDefinition> _schemaCache = new();

    public DatabaseEventSchemaRegistry(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<EventSchemaOptions> options,
        ILogger<DatabaseEventSchemaRegistry> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Unit>> RegisterSchemaAsync<TEvent>(
        EventSchemaDefinition<TEvent> schemaDefinition,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

            // Check if schema already exists
            var existingSchema = await GetSchemaInternalAsync(
                context, 
                schemaDefinition.EventType, 
                schemaDefinition.Version,
                cancellationToken);

            if (existingSchema != null)
            {
                // Validate schema hasn't changed
                if (existingSchema.SchemaHash != schemaDefinition.SchemaHash)
                {
                    return Result<Unit>.Failure(
                        Error.Conflict("SCHEMA_001", "Schema definition has changed for existing version")
                            .WithMetadata("EventType", schemaDefinition.EventType)
                            .WithMetadata("Version", schemaDefinition.Version.ToString()));
                }

                // Already registered with same content
                return Result<Unit>.Success(Unit.Value);
            }

            // Validate compatibility with previous versions if required
            if (_options.Value.ValidateCompatibilityOnRegistration)
            {
                var compatibilityResult = await ValidateRegistrationCompatibilityAsync(
                    context, schemaDefinition, cancellationToken);
                    
                if (compatibilityResult.IsFailure)
                    return Result<Unit>.Failure(compatibilityResult.Error);
            }

            // Store schema in database
            var schemaEntity = new EventSchemaEntity
            {
                Id = Guid.NewGuid(),
                EventType = schemaDefinition.EventType,
                Version = schemaDefinition.Version,
                JsonSchema = schemaDefinition.JsonSchema,
                CreatedAtUtc = DateTime.UtcNow,
                Description = schemaDefinition.Description,
                MetadataJson = schemaDefinition.Metadata != null 
                    ? JsonSerializer.Serialize(schemaDefinition.Metadata) 
                    : null,
                MigrationFromVersion = schemaDefinition.MigrationFromVersion,
                SchemaHash = schemaDefinition.SchemaHash ?? ComputeSchemaHash(schemaDefinition.JsonSchema)
            };

            context.Set<EventSchemaEntity>().Add(schemaEntity);
            await context.SaveChangesAsync(cancellationToken);

            // Update cache
            var cacheKey = GetCacheKey(schemaDefinition.EventType, schemaDefinition.Version);
            _schemaCache.TryAdd(cacheKey, schemaDefinition);

            _logger.LogInformation(
                "Registered event schema {EventType} version {Version}",
                schemaDefinition.EventType,
                schemaDefinition.Version);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("SCHEMA_002", "Failed to register event schema")
                .WithMetadata("EventType", schemaDefinition.EventType)
                .WithMetadata("Version", schemaDefinition.Version.ToString())
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error registering event schema {EventType} version {Version}",
                schemaDefinition.EventType, schemaDefinition.Version);

            return Result<Unit>.Failure(error);
        }
    }

    public async Task<Result<object>> MigrateEventAsync(
        object eventData,
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        if (fromVersion == toVersion)
            return Result<object>.Success(eventData);

        try
        {
            // Get migration path
            var migrationPath = await GetMigrationPathAsync(eventType, fromVersion, toVersion, cancellationToken);
            if (migrationPath.IsFailure)
                return Result<object>.Failure(migrationPath.Error);

            var currentData = eventData;
            var currentVersion = fromVersion;

            // Apply each migration step
            foreach (var step in migrationPath.Value)
            {
                var migrationResult = await ApplyMigrationStepAsync(
                    currentData, eventType, currentVersion, step.ToVersion, cancellationToken);
                    
                if (migrationResult.IsFailure)
                    return Result<object>.Failure(migrationResult.Error);

                currentData = migrationResult.Value;
                currentVersion = step.ToVersion;
            }

            _logger.LogDebug(
                "Successfully migrated {EventType} from version {FromVersion} to {ToVersion}",
                eventType, fromVersion, toVersion);

            return Result<object>.Success(currentData);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("SCHEMA_003", "Event migration failed")
                .WithMetadata("EventType", eventType)
                .WithMetadata("FromVersion", fromVersion.ToString())
                .WithMetadata("ToVersion", toVersion.ToString())
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error migrating event {EventType} from {FromVersion} to {ToVersion}",
                eventType, fromVersion, toVersion);

            return Result<object>.Failure(error);
        }
    }

    public async Task<Result<CompatibilityResult>> ValidateCompatibilityAsync(
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromSchemaResult = await GetSchemaAsync(eventType, fromVersion, cancellationToken);
            var toSchemaResult = await GetSchemaAsync(eventType, toVersion, cancellationToken);

            if (fromSchemaResult.IsFailure)
                return Result<CompatibilityResult>.Failure(fromSchemaResult.Error);

            if (toSchemaResult.IsFailure)
                return Result<CompatibilityResult>.Failure(toSchemaResult.Error);

            // Perform schema compatibility analysis using JSON Schema
            var compatibility = await AnalyzeSchemaCompatibilityAsync(
                fromSchemaResult.Value, toSchemaResult.Value, cancellationToken);

            return Result<CompatibilityResult>.Success(compatibility);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("SCHEMA_004", "Schema compatibility validation failed")
                .WithMetadata("EventType", eventType)
                .WithMetadata("FromVersion", fromVersion.ToString())
                .WithMetadata("ToVersion", toVersion.ToString())
                .WithMetadata("Exception", ex.Message);

            return Result<CompatibilityResult>.Failure(error);
        }
    }

    private async Task<CompatibilityResult> AnalyzeSchemaCompatibilityAsync(
        EventSchemaDefinition fromSchema,
        EventSchemaDefinition toSchema,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation - real-world would use JSON Schema validation libraries
        var issues = new List<CompatibilityIssue>();
        
        try
        {
            var fromSchemaDoc = JsonDocument.Parse(fromSchema.JsonSchema);
            var toSchemaDoc = JsonDocument.Parse(toSchema.JsonSchema);

            // Analyze for breaking changes
            AnalyzeProperties(fromSchemaDoc.RootElement, toSchemaDoc.RootElement, "", issues);

            var level = DetermineCompatibilityLevel(issues);
            var migrationSteps = issues.Any() ? new[] { $"Migrate from v{fromSchema.Version} to v{toSchema.Version}" } : Array.Empty<string>();

            return new CompatibilityResult
            {
                IsCompatible = level != CompatibilityLevel.Breaking,
                Level = level,
                Issues = issues,
                MigrationSteps = migrationSteps
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON schema format during compatibility analysis");
            
            return new CompatibilityResult
            {
                IsCompatible = false,
                Level = CompatibilityLevel.Breaking,
                Issues = new[]
                {
                    new CompatibilityIssue
                    {
                        FieldPath = "root",
                        Type = IssueType.FieldTypeChanged,
                        Description = "Invalid JSON schema format",
                        Suggestion = "Validate JSON schema syntax"
                    }
                }
            };
        }
    }

    private static void AnalyzeProperties(
        JsonElement fromElement,
        JsonElement toElement,
        string path,
        List<CompatibilityIssue> issues)
    {
        if (fromElement.ValueKind != toElement.ValueKind)
        {
            issues.Add(new CompatibilityIssue
            {
                FieldPath = path,
                Type = IssueType.FieldTypeChanged,
                Description = $"Field type changed from {fromElement.ValueKind} to {toElement.ValueKind}",
                Suggestion = "Consider adding a migration to handle type conversion"
            });
            return;
        }

        if (fromElement.ValueKind == JsonValueKind.Object)
        {
            // Check for removed properties
            if (fromElement.TryGetProperty("properties", out var fromProps) &&
                toElement.TryGetProperty("properties", out var toProps))
            {
                foreach (var fromProp in fromProps.EnumerateObject())
                {
                    var fieldPath = string.IsNullOrEmpty(path) ? fromProp.Name : $"{path}.{fromProp.Name}";
                    
                    if (!toProps.TryGetProperty(fromProp.Name, out var toProp))
                    {
                        issues.Add(new CompatibilityIssue
                        {
                            FieldPath = fieldPath,
                            Type = IssueType.FieldRemoved,
                            Description = $"Field '{fromProp.Name}' was removed",
                            Suggestion = "Consider marking field as optional or provide migration logic"
                        });
                    }
                    else
                    {
                        AnalyzeProperties(fromProp.Value, toProp, fieldPath, issues);
                    }
                }

                // Check for new required properties
                if (fromElement.TryGetProperty("required", out var fromRequired) &&
                    toElement.TryGetProperty("required", out var toRequired))
                {
                    var fromRequiredFields = fromRequired.EnumerateArray().Select(e => e.GetString()).ToHashSet();
                    var toRequiredFields = toRequired.EnumerateArray().Select(e => e.GetString()).ToHashSet();

                    foreach (var newRequiredField in toRequiredFields.Except(fromRequiredFields))
                    {
                        issues.Add(new CompatibilityIssue
                        {
                            FieldPath = string.IsNullOrEmpty(path) ? newRequiredField! : $"{path}.{newRequiredField}",
                            Type = IssueType.RequiredFieldAdded,
                            Description = $"New required field '{newRequiredField}' added",
                            Suggestion = "Make field optional or provide default value"
                        });
                    }
                }
            }
        }
    }

    private static CompatibilityLevel DetermineCompatibilityLevel(List<CompatibilityIssue> issues)
    {
        if (!issues.Any())
            return CompatibilityLevel.Full;

        var hasBreakingChanges = issues.Any(i => 
            i.Type == IssueType.FieldRemoved || 
            i.Type == IssueType.RequiredFieldAdded ||
            i.Type == IssueType.FieldTypeChanged);

        return hasBreakingChanges ? CompatibilityLevel.Breaking : CompatibilityLevel.Backward;
    }

    private static string ComputeSchemaHash(string jsonSchema)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonSchema));
        return Convert.ToBase64String(hashBytes);
    }

    private static string GetCacheKey(string eventType, int version) => $"{eventType}:{version}";
}
```

### 3. Event Schema Entity (Database)

**Location**: `src/BuildingBlocks/Infrastructure/Events/EventSchemaEntity.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Database entity for storing event schema definitions
/// </summary>
public sealed class EventSchemaEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = default!;
    public int Version { get; set; }
    public string JsonSchema { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
    public string? MigrationFromVersion { get; set; }
    public string SchemaHash { get; set; } = default!;
    
    // Navigation properties for EF Core
    public List<EventSchemaEntity> MigrationsFrom { get; set; } = new();
    public EventSchemaEntity? MigratesTo { get; set; }
}
```

### 4. Enhanced Event Serializer with Migration

**Location**: Enhance existing `src/Modules/Chat/Infrastructure/Services/EventSourcing/SystemTextJsonEventSerializer.cs`

```csharp
// Add to existing class:
public async Task<TEvent> DeserializeWithMigrationAsync<TEvent>(
    string typeName,
    string payload,
    int eventVersion,
    CancellationToken cancellationToken = default)
    where TEvent : IEvent
{
    var targetType = typeof(TEvent);
    var latestVersion = GetLatestVersion(targetType);
    
    // Deserialize at original version first
    var eventData = await DeserializeAsync(typeName, payload, cancellationToken);
    
    // Migrate to latest version if needed
    if (eventVersion < latestVersion && _schemaRegistry != null)
    {
        var migrationResult = await _schemaRegistry.MigrateEventAsync(
            eventData, typeName, eventVersion, latestVersion, cancellationToken);
            
        if (migrationResult.IsFailure)
            throw new InvalidOperationException($"Event migration failed: {migrationResult.Error.Message}");
            
        return (TEvent)migrationResult.Value;
    }
    
    return (TEvent)eventData;
}
```

### 5. Configuration Options

**Location**: `src/BuildingBlocks/Infrastructure/Events/EventSchemaOptions.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for event schema registry
/// </summary>
public sealed class EventSchemaOptions
{
    public const string ConfigurationSection = "EventSchema";
    
    /// <summary>
    /// Validate schema compatibility when registering new versions
    /// Default: true
    /// </summary>
    public bool ValidateCompatibilityOnRegistration { get; set; } = true;
    
    /// <summary>
    /// Automatically generate JSON schemas from event types on startup
    /// Default: true
    /// </summary>
    public bool AutoGenerateSchemas { get; set; } = true;
    
    /// <summary>
    /// Cache schema definitions in memory for performance
    /// Default: true
    /// </summary>
    public bool EnableSchemaCache { get; set; } = true;
    
    /// <summary>
    /// Schema cache expiration time
    /// Default: 1 hour
    /// </summary>
    public TimeSpan SchemaCacheExpiration { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Require explicit migration functions for breaking changes
    /// Default: true
    /// </summary>
    public bool RequireExplicitMigrations { get; set; } = true;
    
    /// <summary>
    /// Supported schema formats for multi-format support
    /// Default: JSON only
    /// </summary>
    public List<SchemaFormat> SupportedFormats { get; set; } = new() { SchemaFormat.JsonSchema };
}

/// <summary>
/// Supported schema formats
/// </summary>
public enum SchemaFormat
{
    JsonSchema,
    AvroSchema,
    ProtobufSchema
}
```

## Tasks Breakdown

### Phase 1: Schema Registry Foundation (3 hours)
- [ ] Create `IEventSchemaRegistry` interface with Result<T> patterns
- [ ] Design schema definition types and compatibility analysis structures
- [ ] Create `EventSchemaEntity` for database storage
- [ ] Implement basic database schema registry with schema storage

### Phase 2: Migration & Compatibility Logic (2 hours)
- [ ] Implement schema compatibility analysis using JSON Schema comparison
- [ ] Add migration path calculation for multi-step migrations
- [ ] Create migration application logic with error handling
- [ ] Add comprehensive logging and error reporting

### Phase 3: Integration & Enhancement (1 hour)
- [ ] Enhance existing event serializer with migration support
- [ ] Add schema auto-registration on application startup
- [ ] Create configuration options and health checks
- [ ] Write unit tests for migration logic and compatibility validation

## Definition of Done

### Functionality
- [ ] Event schemas registered automatically on application startup
- [ ] Automatic migration between event versions during deserialization
- [ ] Schema compatibility validation prevents breaking changes
- [ ] Schema registry health checks integrated with monitoring

### Quality
- [ ] All operations return `Result<T>` following Epic_03 error patterns
- [ ] Comprehensive logging for schema operations and migrations
- [ ] Migration performance tracked and optimized
- [ ] Unit tests cover migration paths and compatibility edge cases

### Operations
- [ ] Schema registry configuration allows tuning validation strictness
- [ ] Schema diff tools available for code review processes
- [ ] Migration metrics available in monitoring dashboards
- [ ] Zero breaking changes to existing event processing workflows

## Success Criteria

### Technical Success
- Events migrate automatically between versions with < 10ms overhead p95
- Schema compatibility validation catches breaking changes before deployment
- Migration chains work correctly for multi-step version upgrades
- Schema registry remains available with 99.9% uptime

### Operational Success
- Developers can safely evolve event schemas without fear of breaking consumers
- Schema documentation automatically generated and discoverable
- Migration failures are clearly reported with actionable error messages
- Schema registry performance scales with number of registered schemas

This story provides the foundation for safe event evolution while maintaining the reliability established in previous stories.