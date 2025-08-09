namespace BuildingBlocks.Core.Domain.Core.Model.Abstractions;

/// <summary>
/// Marker for stateless domain services (pure domain logic orchestrating multiple aggregates).
/// Avoid application/infrastructure concerns here.
/// </summary>
public interface IDomainService { }