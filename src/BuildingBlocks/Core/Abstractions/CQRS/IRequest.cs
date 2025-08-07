namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Base interface for all requests in CQRS pattern.
/// Provides common behavior for both commands and queries.
/// Follows SRP by focusing solely on request identification.
/// </summary>
public interface IAxonRequest
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// </summary>
    Guid RequestId { get; }
    
    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    DateTime RequestedAt { get; }
}

/// <summary>
/// Interface for requests that return a response.
/// Extends base request with type-safe response handling.
/// For CQRS operations, TResponse should typically be Result&lt;T&gt; to ensure consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this request produces</typeparam>
public interface IAxonRequest<TResponse> : IAxonRequest where TResponse : notnull
{
}