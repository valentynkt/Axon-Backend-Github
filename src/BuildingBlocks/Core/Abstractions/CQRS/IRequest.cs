using System.Diagnostics;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Base interface for all requests in CQRS pattern.
/// Provides common behavior for both commands and queries with W3C TraceContext support and metadata.
/// Follows SRP by focusing solely on request identification, tracing, and metadata.
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
    
    /// <summary>
    /// W3C TraceContext trace identifier.
    /// Returns current activity's trace ID or null if no activity is active.
    /// </summary>
    string? TraceId => Activity.Current?.TraceId.ToString();
    
    /// <summary>
    /// W3C TraceContext span identifier.
    /// Returns current activity's span ID or null if no activity is active.
    /// </summary>
    string? SpanId => Activity.Current?.SpanId.ToString();
    
    /// <summary>
    /// W3C TraceContext parent span identifier.
    /// Returns current activity's parent span ID or null if no parent or activity is active.
    /// </summary>
    string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();
    
    /// <summary>
    /// Immutable metadata dictionary for request context and custom properties.
    /// Enables extensible request enrichment without breaking existing contracts.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
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