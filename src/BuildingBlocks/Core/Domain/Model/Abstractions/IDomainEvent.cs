namespace BuildingBlocks.Core.Domain.Core.Model.Abstractions;

/// <summary>
/// Marker for domain events (in-process, within the domain boundary).
/// Keep lean: no tracing/correlation/transport details here.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}