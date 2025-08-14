namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Lightweight helpers for working with domain events in a transport-agnostic way.
/// Raising events should happen inside aggregates (protected methods).
/// These helpers only build envelopes/metadata for cross-boundary handoff.
/// </summary>
public static class DomainEventExtensions
{
    /// <summary>
    /// Wrap an event with optional metadata (correlation, tenant, headers, etc.).
    /// </summary>
    public static DomainEventEnvelope ToEnvelope(this IDomainEvent @event, DomainEventMetadata? metadata = null)
        => new(@event, metadata);

    /// <summary>
    /// Convenience to attach correlation/causation without constructing metadata by hand.
    /// </summary>
    public static DomainEventEnvelope WithCorrelation(this IDomainEvent @event, string? correlationId, string? causationId = null)
        => new(@event, new DomainEventMetadata { CorrelationId = correlationId, CausationId = causationId });

    /// <summary>
    /// Convenience to attach tenant context.
    /// </summary>
    public static DomainEventEnvelope WithTenant(this IDomainEvent @event, string tenantId)
        => new(@event, new DomainEventMetadata { TenantId = tenantId });

    /// <summary>
    /// Convenience to attach custom headers.
    /// </summary>
    public static DomainEventEnvelope WithHeaders(this IDomainEvent @event, IReadOnlyDictionary<string, string> headers)
        => new(@event, new DomainEventMetadata { Headers = headers });
}