using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Transport-neutral envelope for integration events.
/// Provides consistent structure for cross-boundary event publishing.
/// </summary>
public sealed record IntegrationEventEnvelope(
    Guid EnvelopeId,
    DateTime OccurredAtUtc,
    string Type,                 // Stable event type (resolved name)
    int SchemaVersion,           // Event schema version (default 1 unless overridden)
    IIntegrationEvent Data,      // The actual integration event
    IReadOnlyDictionary<string, object?> Headers // Correlation, Tenant, etc.
);