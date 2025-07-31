namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Represents the result of a rule validation.
/// </summary>
public sealed record RuleResult
{
    /// <summary>
    /// Gets the rule that was executed.
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rule passed.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the violations found by the rule.
    /// </summary>
    public IReadOnlyList<RuleViolation> Violations { get; init; } = Array.Empty<RuleViolation>();

    /// <summary>
    /// Gets the execution time for the rule.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets any error that occurred during rule execution.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful rule result.
    /// </summary>
    public static RuleResult Success(string ruleId, TimeSpan executionTime) =>
        new() { RuleId = ruleId, IsSuccess = true, ExecutionTime = executionTime };

    /// <summary>
    /// Creates a failed rule result with violations.
    /// </summary>
    public static RuleResult Failed(string ruleId, IEnumerable<RuleViolation> violations, TimeSpan executionTime) =>
        new() { RuleId = ruleId, IsSuccess = false, Violations = violations.ToList(), ExecutionTime = executionTime };

    /// <summary>
    /// Creates an error rule result.
    /// </summary>
    public static RuleResult FromError(string ruleId, string error, TimeSpan executionTime) =>
        new() { RuleId = ruleId, IsSuccess = false, Error = error, ExecutionTime = executionTime };
}