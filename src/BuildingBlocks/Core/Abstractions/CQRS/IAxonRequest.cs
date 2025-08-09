using System.Diagnostics;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Base interface for all CQRS requests, shared across layers.
/// Provides request identity, timestamps, W3C TraceContext accessors, and extensible metadata.
/// W3C rule: CorrelationId == TraceId.
/// </summary>
public interface IAxonRequest
{
    /// <summary>Unique identifier for this request instance.</summary>
    Guid RequestId { get; }

    /// <summary>UTC timestamp when the request was created.</summary>
    DateTime RequestedAt { get; }

    /// <summary>W3C TraceContext trace identifier (current Activity TraceId).</summary>
    string? TraceId => Activity.Current?.TraceId.ToString();

    /// <summary>Correlation identifier (same as TraceId per W3C rule).</summary>
    string? CorrelationId => TraceId;

    /// <summary>W3C TraceContext span identifier (current Activity SpanId).</summary>
    string? SpanId => Activity.Current?.SpanId.ToString();

    /// <summary>W3C TraceContext parent span identifier.</summary>
    string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();

    /// <summary>Immutable metadata for request context and custom properties.</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Requests that return a response (typically Result&lt;T&gt;) for consistent error handling.
/// </summary>
public interface IAxonRequest<TResponse> : IAxonRequest where TResponse : notnull
{
}