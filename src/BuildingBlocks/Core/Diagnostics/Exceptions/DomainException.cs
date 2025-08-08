using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Base exception for all domain-related errors.
/// Provides structured error information and integrates with Result pattern.
/// </summary>
[Serializable]
public class DomainException : Exception
{
    /// <summary>
    /// The primary error information
    /// </summary>
    public Error Error { get; }
    
    /// <summary>
    /// All errors (for scenarios with multiple failures)
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }
    
    public DomainException(Error error) 
        : base(error.Message)
    {
        Error = error;
        Errors = new[] { error };
        
        // Preserve error metadata in Exception.Data
        if (error.Metadata != null)
        {
            foreach (var kvp in error.Metadata)
            {
                Data[kvp.Key] = kvp.Value;
            }
        }
    }
    
    public DomainException(string message, Error error)
        : base(message)
    {
        Error = error;
        Errors = new[] { error };
        
        // Preserve error metadata in Exception.Data
        if (error.Metadata != null)
        {
            foreach (var kvp in error.Metadata)
            {
                Data[kvp.Key] = kvp.Value;
            }
        }
    }    
    public DomainException(IEnumerable<Error> errors)
        : base(CreateAggregateMessage(errors))
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        Error = errorList.Count == 1 
            ? errorList[0] 
            : Error.Aggregate(errorList.ToArray());
        Errors = errorList;
    }
    
    public DomainException(string message, IEnumerable<Error> errors)
        : base(message)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
            throw new ArgumentException("At least one error is required", nameof(errors));
            
        Error = errorList.Count == 1 
            ? errorList[0] 
            : Error.Aggregate(errorList.ToArray());
        Errors = errorList;
    }
    
    private static string CreateAggregateMessage(IEnumerable<Error> errors)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
            return "Multiple errors occurred";
            
        if (errorList.Count == 1)
            return errorList[0].Message;
            
        return $"Multiple errors occurred: {string.Join("; ", errorList.Select(e => e.Message))}";
    }
    
    // Custom serialization
    protected DomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Error = (Error)info.GetValue(nameof(Error), typeof(Error))!;
        Errors = (IReadOnlyList<Error>)info.GetValue(nameof(Errors), typeof(IReadOnlyList<Error>))!;
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Error), Error);
        info.AddValue(nameof(Errors), Errors);
    }
}