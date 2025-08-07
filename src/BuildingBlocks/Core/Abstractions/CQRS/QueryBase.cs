using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Abstract base record for queries that return data wrapped in Result pattern.
/// Provides consistent implementation for query identification and timestamps.
/// Uses record type for value equality and immutability benefits.
/// Queries return Result pattern for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this query returns</typeparam>
public abstract record QueryBase<TResponse> : RequestBase<Result<TResponse>>, IQuery<TResponse>
    where TResponse : notnull
{
}