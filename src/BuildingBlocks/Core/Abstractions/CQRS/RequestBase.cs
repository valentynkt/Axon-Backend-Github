// /BuildingBlocks/Core/Abstractions/CQRS/RequestBase.cs
#nullable enable
using System.Collections.ObjectModel;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Minimal base record implementing request identity/timestamps/metadata.
/// </summary>
public abstract record RequestBase : IAxonRequest
{
    public Guid RequestId { get; } = Guid.CreateVersion7();
    public DateTimeOffset RequestedAt { get; } = TimeProvider.System.GetUtcNow();
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    public T WithMetadata<T>(string key, object value) where T : RequestBase
    {
        var m = new Dictionary<string, object>(Metadata) { [key] = value };
        return (T)this with { Metadata = new ReadOnlyDictionary<string, object>(m) };
    }
}