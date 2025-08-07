namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Represents a business rule that can be broken.
/// Business rules encapsulate domain logic and constraints.
/// </summary>
public interface IBusinessRule
{
    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    string Code { get; }
    
    /// <summary>
    /// Human-readable description of the rule
    /// </summary>
    string Message { get; }
    
    /// <summary>
    /// Check if the rule is currently broken
    /// </summary>
    bool IsBroken();
}