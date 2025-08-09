using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Interface for query handlers that return data wrapped in Result pattern.
/// Provides consistent error handling across all query operations.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle</typeparam>
/// <typeparam name="TResponse">The type of response the query produces</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}