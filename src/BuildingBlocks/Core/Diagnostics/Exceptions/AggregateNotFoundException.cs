using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception thrown when an aggregate is not found by its identifier
/// </summary>
[Serializable]
public sealed class AggregateNotFoundException : DomainException
{
    public string AggregateType { get; }
    public string AggregateId { get; }
    
    public AggregateNotFoundException(string aggregateType, string aggregateId)
        : base(CreateError(aggregateType, aggregateId))
    {
        AggregateType = aggregateType;
        AggregateId = aggregateId;
    }
    
    public AggregateNotFoundException(string aggregateType, string aggregateId, string message)
        : base(message, CreateError(aggregateType, aggregateId))
    {
        AggregateType = aggregateType;
        AggregateId = aggregateId;
    }
    
    private static Error CreateError(string aggregateType, string aggregateId)
    {
        var metadata = new Dictionary<string, object>
        {
            ["AggregateType"] = aggregateType,
            ["AggregateId"] = aggregateId
        };
        
        return Error.NotFound(
            $"{aggregateType} with ID '{aggregateId}' was not found",
            "AGGREGATE_NOT_FOUND",
            metadata);
    }
    
    // Serialization constructor
    private AggregateNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        AggregateType = info.GetString(nameof(AggregateType))!;
        AggregateId = info.GetString(nameof(AggregateId))!;
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(AggregateType), AggregateType);
        info.AddValue(nameof(AggregateId), AggregateId);
    }
}