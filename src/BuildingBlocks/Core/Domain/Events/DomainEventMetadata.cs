namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Optional correlation/causation context carried alongside a domain event.
/// Lives in Core to keep cross-module communication consistent, without tying to transport.
/// </summary>
public sealed record DomainEventMetadata
{
    /// <summary>Trace/correlation identifier (e.g., W3C trace id).</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Immediate cause of this event (another event/command id).</summary>
    public string? CausationId { get; init; }

    /// <summary>Multi-tenant context, if any.</summary>
    public string? TenantId { get; init; }

    /// <summary>Actor context, if you capture it in the domain.</summary>
    public string? AxonUserId { get; init; }

    /// <summary>Lightweight, serializable ad-hoc headers (avoid PII here).</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public static DomainEventMetadata Empty { get; } = new();
}