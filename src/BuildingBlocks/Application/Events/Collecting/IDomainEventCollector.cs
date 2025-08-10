using BuildingBlocks.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Application.Events.Collecting;

/// <summary>
/// Collects domain events from tracked aggregates after handler execution.
/// Implementations must NOT cause I/O.
/// </summary>
public interface IDomainEventCollector
{
    /// <summary>
    /// Returns a snapshot of domain events from tracked aggregates.
    /// Optionally clears them to avoid double processing.
    /// </summary>
    IReadOnlyList<IDomainEvent> Collect(DbContext dbContext, bool clear = true);
}