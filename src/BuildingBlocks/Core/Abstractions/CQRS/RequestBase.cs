using MassTransit;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Abstract base record for all CQRS requests.
/// Provides common implementation for request identification and timestamps.
/// Uses record type for value equality and immutability benefits.
/// </summary>
public abstract record RequestBase : IAxonRequest
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// </summary>
    public Guid RequestId { get; } = NewId.NextGuid();
    
    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Abstract base record for requests that return a response.
/// Combines base request functionality with type-safe response handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this request produces</typeparam>
public abstract record RequestBase<TResponse> : RequestBase, IAxonRequest<TResponse> 
    where TResponse : notnull
{
}