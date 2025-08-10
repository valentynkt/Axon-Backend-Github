using System.Text.Json;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Outbox;

/// <summary>
/// Helper for serializing and deserializing domain events in the outbox.
/// Centralizes serialization concerns within the Application layer.
/// </summary>
internal static class OutboxSerialization
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Attempts to deserialize an event from JSON string and type name.
    /// </summary>
    /// <param name="payload">JSON payload</param>
    /// <param name="typeName">Full type name including assembly</param>
    /// <param name="domainEvent">Deserialized event if successful</param>
    /// <param name="error">Error message if failed</param>
    /// <returns>True if deserialization succeeded</returns>
    public static bool TryDeserializeEvent(
        string payload, 
        string typeName, 
        out IDomainEvent? domainEvent, 
        out string? error)
    {
        domainEvent = null;
        error = null;

        try
        {
            // Resolve event type
            var eventType = Type.GetType(typeName);
            if (eventType == null)
            {
                error = $"Event type '{typeName}' not found. Assembly may not be loaded.";
                return false;
            }

            // Deserialize event
            var deserializedObject = JsonSerializer.Deserialize(payload, eventType, SerializerOptions);
            if (deserializedObject is not IDomainEvent evt)
            {
                error = $"Deserialized object is not a valid IDomainEvent for type '{typeName}'";
                return false;
            }

            domainEvent = evt;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON deserialization failed for type '{typeName}': {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Unexpected error deserializing event type '{typeName}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Serialize a domain event to JSON string.
    /// </summary>
    /// <param name="domainEvent">Domain event to serialize</param>
    /// <returns>JSON string representation</returns>
    public static string SerializeEvent(IDomainEvent domainEvent)
    {
        return JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);
    }

    /// <summary>
    /// Get the assembly qualified name for a domain event type.
    /// </summary>
    /// <param name="domainEvent">Domain event</param>
    /// <returns>Assembly qualified type name</returns>
    /// <exception cref="InvalidOperationException">If assembly qualified name cannot be determined</exception>
    public static string GetEventTypeName(IDomainEvent domainEvent)
    {
        return domainEvent.GetType().AssemblyQualifiedName 
            ?? throw new InvalidOperationException($"Could not get assembly qualified name for event type {domainEvent.GetType().FullName}");
    }
}