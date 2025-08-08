using System.Collections.ObjectModel;
using MassTransit;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Abstract base record for all CQRS requests.
/// Provides common implementation for request identification, timestamps, and metadata support.
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
    
    /// <summary>
    /// Immutable metadata dictionary for request context and custom properties.
    /// Initialized as empty readonly dictionary to ensure immutability.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
    
    /// <summary>
    /// Creates a new instance of the request with additional metadata.
    /// Uses copy-on-write semantics to maintain immutability while enabling extensibility.
    /// </summary>
    /// <typeparam name="T">The concrete request type</typeparam>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    /// <returns>New instance with added metadata</returns>
    public T WithMetadata<T>(string key, object value) where T : RequestBase
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return (T)this with { Metadata = new ReadOnlyDictionary<string, object>(newMetadata) };
    }
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