using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Configuration;

/// <summary>
/// Implementation of architecture testing configuration.
/// </summary>
public sealed class ArchitectureSettings : IArchitectureConfiguration
{
    /// <inheritdoc />
    public IRuleConfiguration Rules { get; init; } = new RuleSettings();

    /// <inheritdoc />
    public IExecutionConfiguration Execution { get; init; } = new ExecutionSettings();

    /// <inheritdoc />
    public ILoggingConfiguration Logging { get; init; } = new LoggingSettings();
}

/// <summary>
/// Implementation of rule configuration.
/// </summary>
public sealed class RuleSettings : IRuleConfiguration
{
    /// <inheritdoc />
    public IEnumerable<string> EnabledCategories { get; init; } = 
        new[] { "LayerBoundary", "CQRS", "DDD", "Security", "Quality", "PatternCompliance" };

    /// <inheritdoc />
    public IEnumerable<string> DisabledRules { get; init; } = Array.Empty<string>();

    private readonly Dictionary<string, IDictionary<string, object>> _ruleSettings = new();

    /// <inheritdoc />
    public IDictionary<string, object> GetRuleSettings(string ruleId)
    {
        return _ruleSettings.TryGetValue(ruleId, out var settings) 
            ? settings 
            : new Dictionary<string, object>();
    }

    /// <summary>
    /// Sets configuration for a specific rule.
    /// </summary>
    public void SetRuleSettings(string ruleId, IDictionary<string, object> settings)
    {
        _ruleSettings[ruleId] = settings;
    }
}

/// <summary>
/// Implementation of execution configuration.
/// </summary>
public sealed class ExecutionSettings : IExecutionConfiguration
{
    /// <inheritdoc />
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;

    /// <inheritdoc />
    public TimeSpan RuleTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public bool FailFastOnCritical { get; init; } = true;
}

/// <summary>
/// Implementation of logging configuration.
/// </summary>
public sealed class LoggingSettings : ILoggingConfiguration
{
    /// <inheritdoc />
    public bool EnableDetailedLogging { get; init; } = false;

    /// <inheritdoc />
    public bool EnablePerformanceMetrics { get; init; } = true;
}