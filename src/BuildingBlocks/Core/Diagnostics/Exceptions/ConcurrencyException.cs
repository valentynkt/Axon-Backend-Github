using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception thrown when an optimistic concurrency violation occurs
/// </summary>
[Serializable]
public sealed class ConcurrencyException : DomainException
{
    public string? EntityType { get; }
    public string? EntityId { get; }
    public string? ExpectedVersion { get; }
    public string? ActualVersion { get; }
    
    public ConcurrencyException(string message)
        : base(CreateError(message, null, null, null, null))
    {
    }
    
    public ConcurrencyException(
        string message, 
        string entityType, 
        string entityId, 
        string expectedVersion, 
        string actualVersion)
        : base(CreateError(message, entityType, entityId, expectedVersion, actualVersion))
    {
        EntityType = entityType;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
    
    private static Error CreateError(
        string message, 
        string? entityType, 
        string? entityId, 
        string? expectedVersion, 
        string? actualVersion)
    {
        var metadata = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(entityType))
            metadata["EntityType"] = entityType;
        if (!string.IsNullOrEmpty(entityId))
            metadata["EntityId"] = entityId;
        if (!string.IsNullOrEmpty(expectedVersion))
            metadata["ExpectedVersion"] = expectedVersion;
        if (!string.IsNullOrEmpty(actualVersion))
            metadata["ActualVersion"] = actualVersion;
        
        return Error.Conflict(
            message,
            "CONCURRENCY_VIOLATION",
            metadata.Count > 0 ? metadata : null);
    }    
    // Serialization constructor
    private ConcurrencyException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        EntityType = info.GetString(nameof(EntityType));
        EntityId = info.GetString(nameof(EntityId));
        ExpectedVersion = info.GetString(nameof(ExpectedVersion));
        ActualVersion = info.GetString(nameof(ActualVersion));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(EntityType), EntityType);
        info.AddValue(nameof(EntityId), EntityId);
        info.AddValue(nameof(ExpectedVersion), ExpectedVersion);
        info.AddValue(nameof(ActualVersion), ActualVersion);
    }
}