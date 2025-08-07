using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for queries that return data without modifying system state wrapped in Result pattern.
/// Queries represent read operations following CQS principle.
/// All query results are wrapped in Result&lt;T&gt; for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of data this query returns</typeparam>
public interface IQuery<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
}