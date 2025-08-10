using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Factory for creating integration event envelopes with proper metadata.
/// Encapsulates envelope creation logic and header population.
/// </summary>
public interface IIntegrationEventEnvelopeFactory
{
    /// <summary>
    /// Create envelopes from a domain event already mapped to 0..N integration events.
    /// </summary>
    /// <param name="domainEvent">Source domain event</param>
    /// <param name="integrationEvents">Mapped integration events</param>
    /// <param name="context">Context with trace/request/tenant metadata when available</param>
    /// <returns>List of envelopes ready for publishing</returns>
    IReadOnlyList<IntegrationEventEnvelope> Create(
        IDomainEvent domainEvent,
        IEnumerable<IIntegrationEvent> integrationEvents,
        IntegrationEnvelopeContext? context
    );
}

/// <summary>
/// Context captured from Outbox entry or current Activity.
/// Provides metadata for envelope headers.
/// </summary>
public sealed record IntegrationEnvelopeContext(
    string? TraceId,
    Guid? RequestId,
    string? TenantId,
    IReadOnlyDictionary<string, object>? Metadata,
    Guid? OutboxEntryId = null,
    Guid? TransactionId = null
);