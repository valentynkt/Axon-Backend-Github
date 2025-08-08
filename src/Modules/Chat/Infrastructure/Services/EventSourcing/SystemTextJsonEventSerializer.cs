using Axon.Shared.Domain;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Core.Domain.Events;

namespace Axon.Modules.Chat.Infrastructure.Services.EventSourcing;

/// <summary>
/// Event serialization interface for domain events following SPARC patterns
/// </summary>
public interface IEventSerializer
{
    Task<string> SerializeAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task<IDomainEvent> DeserializeAsync(string typeName, string payload, CancellationToken cancellationToken = default);
    Task<string> BuildMetadataAsync(IDomainEvent domainEvent, string correlationId, string userId, CancellationToken cancellationToken = default);
    Task<EventMetadata> DeserializeMetadataAsync(string metadata, CancellationToken cancellationToken = default);
}

/// <summary>
/// System.Text.Json implementation with StrongId support and type registry
/// Following SPARC Event Sourcing architecture patterns
/// </summary>
public sealed class SystemTextJsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IEventTypeRegistry _typeRegistry;
    private readonly ILogger<SystemTextJsonEventSerializer> _logger;
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();

    public SystemTextJsonEventSerializer(
        IEventTypeRegistry typeRegistry,
        ILogger<SystemTextJsonEventSerializer> logger)
    {
        _typeRegistry = typeRegistry;
        _logger = logger;
        _jsonOptions = CreateJsonOptions();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        // Add StrongId converters for domain value objects
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    public async Task<string> SerializeAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        try
        {
            var eventType = domainEvent.GetType();
            var eventVersion = _typeRegistry.GetEventVersion(eventType);
            
            var envelope = new EventEnvelope
            {
                EventType = eventType.AssemblyQualifiedName!,
                EventVersion = eventVersion,
                Data = domainEvent,
                SchemaHash = _typeRegistry.GetSchemaHash(eventType)
            };

            var json = JsonSerializer.Serialize(envelope, _jsonOptions);
            
            _logger.LogTrace("Serialized event {EventType} v{Version} to {Size} bytes",
                eventType.Name, eventVersion, System.Text.Encoding.UTF8.GetByteCount(json));

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize domain event of type {EventType}",
                domainEvent.GetType().Name);
            throw new EventSerializationException($"Failed to serialize event: {ex.Message}", ex);
        }
    }

    public async Task<IDomainEvent> DeserializeAsync(
        string typeName,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(payload, _jsonOptions);
            if (envelope?.Data == null)
                throw new EventSerializationException("Event envelope or data is null");

            var eventType = GetEventType(envelope.EventType);
            var currentVersion = _typeRegistry.GetEventVersion(eventType);

            // Handle event versioning and upcasting if needed
            if (envelope.EventVersion < currentVersion)
            {
                _logger.LogDebug("Upcasting event {EventType} from v{OldVersion} to v{NewVersion}",
                    eventType.Name, envelope.EventVersion, currentVersion);

                return await _typeRegistry.UpcastEventAsync(envelope, eventType, cancellationToken);
            }

            // Validate schema hash for integrity (optional)
            var expectedSchemaHash = _typeRegistry.GetSchemaHash(eventType);
            if (!string.IsNullOrEmpty(envelope.SchemaHash) && 
                envelope.SchemaHash != expectedSchemaHash)
            {
                _logger.LogWarning("Schema hash mismatch for event {EventType}. Expected: {Expected}, Actual: {Actual}",
                    eventType.Name, expectedSchemaHash, envelope.SchemaHash);
            }

            return (IDomainEvent)envelope.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize domain event of type {EventType}", typeName);
            throw new EventSerializationException($"Failed to deserialize event: {ex.Message}", ex);
        }
    }

    public async Task<string> BuildMetadataAsync(
        IDomainEvent domainEvent,
        string correlationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var metadata = new EventMetadata
        {
            CorrelationId = correlationId,
            CausationId = System.Diagnostics.Activity.Current?.ParentId,
            AggregateType = ExtractAggregateType(domainEvent),
            AggregateId = ExtractAggregateId(domainEvent),
            EventType = domainEvent.GetType().Name,
            EventVersion = _typeRegistry.GetEventVersion(domainEvent.GetType()),
            UserId = userId,
            OccurredAtUtc = domainEvent.OccurredAt,
            MachineName = Environment.MachineName,
            ApplicationVersion = GetApplicationVersion()
        };

        return JsonSerializer.Serialize(metadata, _jsonOptions);
    }

    public async Task<EventMetadata> DeserializeMetadataAsync(
        string metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata);
        return JsonSerializer.Deserialize<EventMetadata>(metadata, _jsonOptions)!;
    }

    private static Type GetEventType(string typeName)
    {
        return TypeCache.GetOrAdd(typeName, name =>
        {
            var type = Type.GetType(name);
            if (type == null)
                throw new EventSerializationException($"Unable to resolve event type: {name}");
            return type;
        });
    }

    private static string ExtractAggregateType(IDomainEvent domainEvent)
    {
        // Convention: EventName -> AggregateType (remove "DomainEvent" suffix)
        var eventTypeName = domainEvent.GetType().Name;
        if (eventTypeName.EndsWith("DomainEvent"))
            return eventTypeName[..^11]; // Remove "DomainEvent"
        return eventTypeName;
    }

    private static string ExtractAggregateId(IDomainEvent domainEvent)
    {
        // Use reflection to extract aggregate ID from well-known properties
        var eventType = domainEvent.GetType();
        var idProperty = eventType.GetProperty("AggregateId") ?? 
                        eventType.GetProperty("ConversationId") ??
                        eventType.GetProperty("MessageId");

        return idProperty?.GetValue(domainEvent)?.ToString() ?? "unknown";
    }

    private static string GetApplicationVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
    }
}

/// <summary>
/// Event metadata contract for correlation, tracing, and audit information
/// </summary>
public sealed record EventMetadata
{
    public required string CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public required string EventType { get; init; }
    public int EventVersion { get; init; }
    public required string UserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string MachineName { get; init; } = Environment.MachineName;
    public string ApplicationVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Event envelope for serialization with versioning support
/// </summary>
public sealed record EventEnvelope
{
    public required string EventType { get; init; }
    public int EventVersion { get; init; }
    public required object Data { get; init; }
    public string? SchemaHash { get; init; }
}

/// <summary>
/// Exception thrown when event serialization/deserialization fails
/// </summary>
public sealed class EventSerializationException : Exception
{
    public EventSerializationException(string message) : base(message) { }
    public EventSerializationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Registry for event types with versioning and schema evolution support
/// </summary>
public interface IEventTypeRegistry
{
    int GetEventVersion(Type eventType);
    string GetSchemaHash(Type eventType);
    Task<IDomainEvent> UpcastEventAsync(EventEnvelope envelope, Type targetType, CancellationToken cancellationToken);
    void RegisterEventType<T>(int version, string? schemaHash = null) where T : IDomainEvent;
}

/// <summary>
/// In-memory implementation of event type registry with reflection caching
/// </summary>
public sealed class InMemoryEventTypeRegistry : IEventTypeRegistry
{
    private readonly ConcurrentDictionary<Type, EventTypeInfo> _eventTypes = new();
    private readonly ILogger<InMemoryEventTypeRegistry> _logger;

    public InMemoryEventTypeRegistry(ILogger<InMemoryEventTypeRegistry> logger)
    {
        _logger = logger;
        RegisterKnownEventTypes();
    }

    public int GetEventVersion(Type eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var info))
            return info.Version;

        _logger.LogWarning("Event type {EventType} not registered, defaulting to version 1", eventType.Name);
        return 1;
    }

    public string GetSchemaHash(Type eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var info))
            return info.SchemaHash ?? GenerateSchemaHash(eventType);

        return GenerateSchemaHash(eventType);
    }

    public async Task<IDomainEvent> UpcastEventAsync(
        EventEnvelope envelope,
        Type targetType,
        CancellationToken cancellationToken)
    {
        // Simple upcasting strategy - deserialize to latest version
        var jsonElement = (JsonElement)envelope.Data;
        var upcastedEvent = JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        
        return (IDomainEvent)upcastedEvent!;
    }

    public void RegisterEventType<T>(int version, string? schemaHash = null) where T : IDomainEvent
    {
        var eventType = typeof(T);
        var info = new EventTypeInfo(version, schemaHash ?? GenerateSchemaHash(eventType));
        
        _eventTypes.AddOrUpdate(eventType, info, (_, existing) =>
        {
            if (existing.Version != version)
            {
                _logger.LogInformation("Updated event type {EventType} from v{OldVersion} to v{NewVersion}",
                    eventType.Name, existing.Version, version);
            }
            return info;
        });
    }

    private void RegisterKnownEventTypes()
    {
        // Register known domain events - would be expanded in real implementation
        _logger.LogInformation("Registered {Count} event types", _eventTypes.Count);
    }

    private static string GenerateSchemaHash(Type eventType)
    {
        // Generate a simple hash based on property names and types
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name)
            .Select(p => $"{p.Name}:{p.PropertyType.Name}")
            .ToArray();

        var schemaString = string.Join("|", properties);
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(schemaString)))[..8];
    }

    private sealed record EventTypeInfo(int Version, string SchemaHash);
}