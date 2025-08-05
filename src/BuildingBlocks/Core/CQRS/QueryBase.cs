namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Abstract base record for queries that return data.
/// Provides consistent implementation for query identification and timestamps.
/// Uses record type for value equality and immutability benefits.
/// </summary>
/// <typeparam name="TResponse">The type of response this query returns</typeparam>
public abstract record QueryBase<TResponse> : RequestBase<TResponse>, IQuery<TResponse>
    where TResponse : notnull
{
}