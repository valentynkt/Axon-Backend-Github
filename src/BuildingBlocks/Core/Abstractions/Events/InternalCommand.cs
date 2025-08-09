using System.Collections.ObjectModel;
using BuildingBlocks.Core.Abstractions.CQRS;
using MassTransit;

namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Base record for internal commands that combine event and command characteristics.
/// Provides consistent implementation for internal command processing with proper initialization.
/// </summary>
public abstract record InternalCommand : IInternalCommand, ICommand
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; } = NewId.NextGuid();
    
    /// <summary>
    /// When this event occurred in UTC.
    /// </summary>
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// The type name of this event for serialization and routing.
    /// </summary>
    public string EventType { get; }
    
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
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Protected constructor that sets the event type based on the concrete implementation.
    /// </summary>
    protected InternalCommand()
    {
        Metadata = new ReadOnlyDictionary<string, object>(new());
        EventType = GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;
    }

    /// <summary>
    /// Protected constructor with metadata support.
    /// </summary>
    protected InternalCommand(IReadOnlyDictionary<string, object>? metadata)
    {
        Metadata = metadata ?? new ReadOnlyDictionary<string, object>(new());
        EventType = GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;
    }
}