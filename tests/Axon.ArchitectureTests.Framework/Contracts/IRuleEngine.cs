namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Defines a contract for executing architecture rules with parallel processing support.
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Registers a rule for execution.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    void RegisterRule(IArchitectureRule rule);

    /// <summary>
    /// Registers multiple rules for execution.
    /// </summary>
    /// <param name="rules">The rules to register.</param>
    void RegisterRules(IEnumerable<IArchitectureRule> rules);

    /// <summary>
    /// Executes all registered rules against the provided context.
    /// </summary>
    /// <param name="context">The architecture context.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task representing the execution results.</returns>
    Task<EngineResult> ExecuteAsync(IArchitectureContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes specific rules by category against the provided context.
    /// </summary>
    /// <param name="context">The architecture context.</param>
    /// <param name="categories">The rule categories to execute.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task representing the execution results.</returns>
    Task<EngineResult> ExecuteByCategoryAsync(IArchitectureContext context, IEnumerable<string> categories, CancellationToken cancellationToken = default);
}