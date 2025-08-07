using BuildingBlocks.Core.CQRS;

namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Interface for paginated queries that combine query and pagination concerns.
/// Extends IQuery to leverage CQRS patterns while providing pagination metadata.
/// Follows ISP by composing pagination and query interfaces.
/// </summary>
/// <typeparam name="TResponse">The type of paginated response this query returns</typeparam>
public interface IPageQuery<TResponse> : IPageRequest, IQuery<TResponse>
    where TResponse : class
{
}