using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception thrown when an aggregate invariant is violated
/// </summary>
[Serializable]
public sealed class InvariantViolationException : DomainException
{
    public string? AggregateType { get; }
    public string? InvariantName { get; }
    
    public InvariantViolationException(string message)
        : base(CreateError(message, null, null))
    {
    }
    
    public InvariantViolationException(string message, string aggregateType, string invariantName)
        : base(CreateError(message, aggregateType, invariantName))
    {
        AggregateType = aggregateType;
        InvariantName = invariantName;
    }
    
    private static Error CreateError(string message, string? aggregateType, string? invariantName)
    {
        var metadata = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(aggregateType))
            metadata["AggregateType"] = aggregateType;
        if (!string.IsNullOrEmpty(invariantName))
            metadata["InvariantName"] = invariantName;
        
        return Error.BusinessRule(
            message,
            "INVARIANT_VIOLATION",
            metadata.Count > 0 ? metadata : null);
    }
    
    // Serialization constructor
    private InvariantViolationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        AggregateType = info.GetString(nameof(AggregateType));
        InvariantName = info.GetString(nameof(InvariantName));
    }
    
    #pragma warning disable SYSLIB0051
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(AggregateType), AggregateType);
        info.AddValue(nameof(InvariantName), InvariantName);
    }
    #pragma warning restore SYSLIB0051
}