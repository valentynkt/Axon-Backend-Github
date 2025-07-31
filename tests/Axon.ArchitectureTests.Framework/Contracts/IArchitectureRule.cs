namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Defines a contract for architecture validation rules that can be executed by the rule engine.
/// </summary>
public interface IArchitectureRule
{
    /// <summary>
    /// Gets the unique identifier for this rule.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Gets the human-readable name of this rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this rule validates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category this rule belongs to (e.g., "Dependencies", "CQRS", "Security").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets the severity level for violations of this rule.
    /// </summary>
    RuleSeverity Severity { get; }

    /// <summary>
    /// Validates the rule against the provided architecture context.
    /// </summary>
    /// <param name="context">The architecture context containing assemblies and configuration.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task representing the validation result.</returns>
    Task<RuleResult> ValidateAsync(IArchitectureContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Severity levels for rule violations.
/// </summary>
public enum RuleSeverity
{
    /// <summary>
    /// Informational - does not fail the build.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - reported but does not fail the build.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - fails the build.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - immediate failure, stops further processing.
    /// </summary>
    Critical
}