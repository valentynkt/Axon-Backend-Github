namespace Axon.Modules.Chat.Infrastructure.Persistence.Entities;

/// <summary>
/// Event correlation entity for tracking causation chains
/// Enables distributed tracing and debugging per SPARC patterns
/// </summary>
public sealed class EventCorrelation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string CorrelationId { get; init; }
    public required string EventType { get; init; }
    public required string AggregateId { get; init; }
    public string? CausationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public required string MachineName { get; init; }
}