using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for queries that return data without modifying system state.
/// Queries represent read operations following CQS principle.
/// All queries must return a non-null response to ensure type safety.
/// </summary>
/// <typeparam name="TResponse">The type of data this query returns</typeparam>
public interface IQuery<out TResponse> : IAxonRequest<TResponse>, MediatR.IRequest<TResponse>
    where TResponse : notnull
{
}