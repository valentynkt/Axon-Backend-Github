using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Policy for computing consistent headers on integration event envelopes.
/// Ensures predictable metadata, correlation/causation tracking, and stable idempotency keys.
/// </summary>
public interface IEnvelopeHeaderPolicy
{
    /// <summary>
    /// Apply header policy to compute final envelope headers.
    /// Merges existing headers with policy-driven headers, with policy taking precedence.
    /// </summary>
    /// <param name="domainEvent">Source domain event (may be null for direct integration events)</param>
    /// <param name="eventTypeName">Resolved event type name</param>
    /// <param name="schemaVersion">Event schema version</param>
    /// <param name="context">Envelope context (from outbox or request scope)</param>
    /// <param name="existingHeaders">Pre-existing headers to merge</param>
    /// <param name="payloadHashProvider">Lazy provider for payload hash (used for fallback idempotency key)</param>
    /// <returns>Final headers dictionary with all required headers applied</returns>
    IReadOnlyDictionary<string, object> Apply(
        IDomainEvent? domainEvent,
        string eventTypeName,
        string schemaVersion,
        IntegrationEnvelopeContext? context,
        IReadOnlyDictionary<string, object>? existingHeaders,
        Func<string> payloadHashProvider
    );
}