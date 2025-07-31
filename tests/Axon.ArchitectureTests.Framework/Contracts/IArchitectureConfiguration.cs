namespace Axon.ArchitectureTests.Framework.Contracts;

/// <summary>
/// Defines a contract for architecture testing configuration.
/// </summary>
public interface IArchitectureConfiguration
{
    /// <summary>
    /// Gets the rule configuration settings.
    /// </summary>
    IRuleConfiguration Rules { get; }

    /// <summary>
    /// Gets the execution configuration settings.
    /// </summary>
    IExecutionConfiguration Execution { get; }

    /// <summary>
    /// Gets the logging configuration settings.
    /// </summary>
    ILoggingConfiguration Logging { get; }
}

/// <summary>
/// Configuration for individual rules.
/// </summary>
public interface IRuleConfiguration
{
    /// <summary>
    /// Gets the enabled rule categories.
    /// </summary>
    IEnumerable<string> EnabledCategories { get; }

    /// <summary>
    /// Gets the disabled rule IDs.
    /// </summary>
    IEnumerable<string> DisabledRules { get; }

    /// <summary>
    /// Gets rule-specific configuration values.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <returns>Configuration dictionary for the rule.</returns>
    IDictionary<string, object> GetRuleSettings(string ruleId);
}

/// <summary>
/// Configuration for rule execution.
/// </summary>
public interface IExecutionConfiguration
{
    /// <summary>
    /// Gets the maximum degree of parallelism for rule execution.
    /// </summary>
    int MaxDegreeOfParallelism { get; }

    /// <summary>
    /// Gets the timeout for individual rule execution.
    /// </summary>
    TimeSpan RuleTimeout { get; }

    /// <summary>
    /// Gets a value indicating whether to fail fast on critical errors.
    /// </summary>
    bool FailFastOnCritical { get; }
}

/// <summary>
/// Configuration for logging.
/// </summary>
public interface ILoggingConfiguration
{
    /// <summary>
    /// Gets a value indicating whether detailed logging is enabled.
    /// </summary>
    bool EnableDetailedLogging { get; }

    /// <summary>
    /// Gets a value indicating whether performance metrics should be collected.
    /// </summary>
    bool EnablePerformanceMetrics { get; }
}