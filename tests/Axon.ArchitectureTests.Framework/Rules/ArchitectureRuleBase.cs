using System.Diagnostics;
using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Rules;

/// <summary>
/// Base class for architecture rules providing common functionality.
/// </summary>
public abstract class ArchitectureRuleBase : IArchitectureRule
{
    /// <inheritdoc />
    public abstract string RuleId { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract string Category { get; }

    /// <inheritdoc />
    public virtual RuleSeverity Severity => RuleSeverity.Error;

    /// <inheritdoc />
    public async Task<RuleResult> ValidateAsync(IArchitectureContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var violations = await ExecuteValidationAsync(context, cancellationToken);
            stopwatch.Stop();

            return violations.Any() 
                ? RuleResult.Failed(RuleId, violations, stopwatch.Elapsed)
                : RuleResult.Success(RuleId, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return RuleResult.FromError(RuleId, ex.Message, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Executes the rule validation logic.
    /// </summary>
    /// <param name="context">The architecture context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of rule violations.</returns>
    protected abstract Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a rule violation.
    /// </summary>
    protected RuleViolation CreateViolation(Type type, string message, string? suggestedFix = null) =>
        new()
        {
            TypeName = type.FullName ?? type.Name,
            AssemblyName = type.Assembly.GetName().Name ?? "Unknown",
            Message = message,
            Severity = Severity,
            SuggestedFix = suggestedFix
        };
}