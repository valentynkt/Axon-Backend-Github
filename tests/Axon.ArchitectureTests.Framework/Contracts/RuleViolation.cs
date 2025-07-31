namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Represents a violation of an architecture rule.
/// </summary>
public sealed record RuleViolation
{
    /// <summary>
    /// Gets the type that violates the rule.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets the assembly containing the violating type.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Gets the violation message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the severity of this violation.
    /// </summary>
    public required RuleSeverity Severity { get; init; }

    /// <summary>
    /// Gets additional context about the violation.
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the suggested fix for this violation.
    /// </summary>
    public string? SuggestedFix { get; init; }
}