// /BuildingBlocks/Core/Abstractions/CQRS/IAxonRequest.cs
#nullable enable
using System.Diagnostics;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Base interface for all CQRS requests, shared across layers.
/// Provides request identity, timestamps, W3C TraceContext accessors, and extensible metadata.
/// CorrelationId prefers Activity.TraceId, falls back to RequestId for stability.
/// </summary>
public interface IAxonRequest
{
    Guid RequestId { get; }
    DateTimeOffset RequestedAt { get; }

    string? TraceId => Activity.Current?.TraceId.ToString();
    string CorrelationId => TraceId ?? RequestId.ToString();

    string? SpanId => Activity.Current?.SpanId.ToString();
    string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();

    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Requests that return a response (typically Result&lt;T&gt;).
/// </summary>
public interface IAxonRequest<out TResponse> : IAxonRequest where TResponse : notnull { }