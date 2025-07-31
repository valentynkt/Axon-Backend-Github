namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Represents the result of executing multiple architecture rules.
/// </summary>
public sealed record EngineResult
{
    /// <summary>
    /// Gets the individual rule results.
    /// </summary>
    public IReadOnlyList<RuleResult> RuleResults { get; init; } = Array.Empty<RuleResult>();

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether all rules passed.
    /// </summary>
    public bool IsSuccess => RuleResults.All(r => r.IsSuccess);

    /// <summary>
    /// Gets the total number of violations across all rules.
    /// </summary>
    public int TotalViolations => RuleResults.Sum(r => r.Violations.Count);

    /// <summary>
    /// Gets violations grouped by severity.
    /// </summary>
    public IReadOnlyDictionary<RuleSeverity, int> ViolationsBySeverity =>
        RuleResults
            .SelectMany(r => r.Violations)
            .GroupBy(v => v.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Gets a summary of the execution results.
    /// </summary>
    public string GetSummary() =>
        $"Executed {RuleResults.Count} rules in {TotalExecutionTime.TotalMilliseconds:F0}ms. " +
        $"Success: {RuleResults.Count(r => r.IsSuccess)}, Failed: {RuleResults.Count(r => !r.IsSuccess)}, " +
        $"Total Violations: {TotalViolations}";
}