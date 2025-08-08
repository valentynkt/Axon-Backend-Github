namespace BuildingBlocks.Core.Abstractions.Pagination;

// Marker interface for CQRS queries that return paged results.
// Intentionally empty to avoid coupling with a specific mediator/IQuery type.
public interface IPageQuery<out TResponse> { }