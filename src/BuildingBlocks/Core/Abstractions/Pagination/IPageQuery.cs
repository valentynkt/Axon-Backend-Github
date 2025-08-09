namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Marker interface for CQRS queries that return paged results.
/// Intentionally empty to avoid coupling with a specific mediator/IQuery type.
/// Enables identification of page queries without enforcing implementation details.
/// </summary>
/// <typeparam name="TResponse">The type of response this page query returns</typeparam>
public interface IPageQuery<out TResponse> 
{ 
}