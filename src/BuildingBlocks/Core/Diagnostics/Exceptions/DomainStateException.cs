using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Exception thrown when an invalid state transition is attempted
/// </summary>
[Serializable]
public sealed class DomainStateException : DomainException
{
    public string? EntityType { get; }
    public string? CurrentState { get; }
    public string? AttemptedState { get; }
    
    public DomainStateException(string message)
        : base(CreateError(message, null, null, null))
    {
    }
    
    public DomainStateException(
        string message, 
        string entityType, 
        string currentState, 
        string attemptedState)
        : base(CreateError(message, entityType, currentState, attemptedState))
    {
        EntityType = entityType;
        CurrentState = currentState;
        AttemptedState = attemptedState;
    }
    
    private static Error CreateError(
        string message, 
        string? entityType, 
        string? currentState, 
        string? attemptedState)
    {
        var metadata = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(entityType))
            metadata["EntityType"] = entityType;
        if (!string.IsNullOrEmpty(currentState))
            metadata["CurrentState"] = currentState;
        if (!string.IsNullOrEmpty(attemptedState))
            metadata["AttemptedState"] = attemptedState;
        
        return Error.BusinessRule(
            message,
            "INVALID_STATE_TRANSITION",
            metadata.Count > 0 ? metadata : null);
    }
    
    // Serialization constructor
    private DomainStateException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        EntityType = info.GetString(nameof(EntityType));
        CurrentState = info.GetString(nameof(CurrentState));
        AttemptedState = info.GetString(nameof(AttemptedState));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(EntityType), EntityType);
        info.AddValue(nameof(CurrentState), CurrentState);
        info.AddValue(nameof(AttemptedState), AttemptedState);
    }
}