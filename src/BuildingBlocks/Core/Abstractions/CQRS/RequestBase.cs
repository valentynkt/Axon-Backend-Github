using System.Collections.ObjectModel;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Abstract base record for all CQRS requests (shared, layer-agnostic).
/// Provides identity, timestamps, and immutable metadata with copy-on-write enrichment.
/// </summary>
public abstract record RequestBase : IAxonRequest
{
    /// <summary>
    /// Unique identifier for this request instance.
    /// Uses time-ordered v7 GUID on .NET 8+, otherwise falls back to random GUID.
    /// </summary>
    public Guid RequestId { get; } =
#if NET8_0_OR_GREATER
        Guid.CreateVersion7();
#else
        Guid.NewGuid();
#endif

    /// <summary>UTC timestamp when the request was created.</summary>
    public DateTime RequestedAt { get; } = DateTime.UtcNow;

    /// <summary>Immutable metadata for request context and custom properties.</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new instance with an added/updated metadata entry (copy-on-write).
    /// </summary>
    public T WithMetadata<T>(string key, object value) where T : RequestBase
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return (T)this with { Metadata = new ReadOnlyDictionary<string, object>(newMetadata) };
    }
}

/// <summary>Base record for requests that return a response.</summary>
public abstract record RequestBase<TResponse> : RequestBase, IAxonRequest<TResponse>
    where TResponse : notnull
{
}