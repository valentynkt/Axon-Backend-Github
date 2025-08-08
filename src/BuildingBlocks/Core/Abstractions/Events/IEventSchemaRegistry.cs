using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Central registry for event schema management and versioning.
/// Provides type-safe schema operations with comprehensive validation and migration support.
/// Follows repository pattern with Result-based error handling for resilient operations.
/// </summary>
public interface IEventSchemaRegistry
{
    /// <summary>
    /// Register a new event schema with version and migration information.
    /// Validates schema compatibility with existing versions and stores migration paths.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register schema for</typeparam>
    /// <param name="schemaDefinition">Complete schema definition with metadata</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or detailed failure information</returns>
    Task<Result<Unit>> RegisterSchemaAsync<TEvent>(
        EventSchemaDefinition<TEvent> schemaDefinition,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Register a schema definition without type safety for dynamic scenarios.
    /// Used when event type is not known at compile time.
    /// </summary>
    /// <param name="schemaDefinition">Untyped schema definition</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result indicating success or detailed failure information</returns>
    Task<Result<Unit>> RegisterSchemaAsync(
        EventSchemaDefinition schemaDefinition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve the current schema definition for a specific event type.
    /// Returns the latest version by default, or a specific version if requested.
    /// </summary>
    /// <param name="eventType">The event type name to lookup</param>
    /// <param name="version">Specific version to retrieve (optional - defaults to latest)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the schema definition or detailed failure information</returns>
    Task<Result<EventSchemaDefinition>> GetSchemaAsync(
        string eventType,
        int? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get strongly-typed schema definition for compile-time safety.
    /// </summary>
    /// <typeparam name="TEvent">The event type to get schema for</typeparam>
    /// <param name="version">Specific version to retrieve (optional - defaults to latest)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the typed schema definition</returns>
    Task<Result<EventSchemaDefinition<TEvent>>> GetSchemaAsync<TEvent>(
        int? version = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Validate compatibility between two schema versions.
    /// Determines if events can be migrated between versions and identifies potential issues.
    /// </summary>
    /// <param name="eventType">The event type to validate</param>
    /// <param name="fromVersion">Source schema version</param>
    /// <param name="toVersion">Target schema version</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed compatibility analysis</returns>
    Task<Result<CompatibilityResult>> ValidateCompatibilityAsync(
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrate event data from one schema version to another.
    /// Applies transformation rules defined during schema registration.
    /// </summary>
    /// <param name="eventData">The event data to migrate</param>
    /// <param name="eventType">The event type being migrated</param>
    /// <param name="fromVersion">Current schema version of the data</param>
    /// <param name="toVersion">Target schema version</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the migrated event data</returns>
    Task<Result<object>> MigrateEventAsync(
        object eventData,
        string eventType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrate strongly-typed event data between versions.
    /// Provides compile-time safety for known event types.
    /// </summary>
    /// <typeparam name="TEvent">The event type to migrate</typeparam>
    /// <param name="eventData">The typed event data to migrate</param>
    /// <param name="fromVersion">Current schema version of the data</param>
    /// <param name="toVersion">Target schema version</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the migrated typed event data</returns>
    Task<Result<TEvent>> MigrateEventAsync<TEvent>(
        TEvent eventData,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Get comprehensive health status of the schema registry.
    /// Includes connectivity, performance metrics, and operational status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing detailed health information</returns>
    Task<Result<SchemaRegistryHealth>> GetHealthAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all registered event types with their version information.
    /// Useful for discovery and maintenance operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the list of registered event types</returns>
    Task<Result<IReadOnlyList<EventTypeInfo>>> ListEventTypesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions for a specific event type.
    /// Returns complete version history with metadata.
    /// </summary>
    /// <param name="eventType">The event type to get versions for</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing the list of schema versions</returns>
    Task<Result<IReadOnlyList<SchemaVersionInfo>>> GetSchemaVersionsAsync(
        string eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a specific event instance against its registered schema.
    /// Ensures data integrity and schema compliance.
    /// </summary>
    /// <param name="eventData">The event data to validate</param>
    /// <param name="eventType">The event type for validation</param>
    /// <param name="version">Schema version to validate against (optional - defaults to latest)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing validation results</returns>
    Task<Result<SchemaValidationResult>> ValidateEventAsync(
        object eventData,
        string eventType,
        int? version = null,
        CancellationToken cancellationToken = default);
}