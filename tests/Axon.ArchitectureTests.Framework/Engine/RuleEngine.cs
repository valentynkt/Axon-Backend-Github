using System.Collections.Concurrent;
using System.Diagnostics;
using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Engine;

/// <summary>
/// Rule engine that executes architecture rules with parallel processing support.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private readonly ConcurrentBag<IArchitectureRule> _rules = new();

    /// <inheritdoc />
    public void RegisterRule(IArchitectureRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    /// <inheritdoc />
    public void RegisterRules(IEnumerable<IArchitectureRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            _rules.Add(rule);
        }
    }

    /// <inheritdoc />
    public async Task<EngineResult> ExecuteAsync(IArchitectureContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var allRules = _rules.ToList();
        
        if (!allRules.Any())
        {
            return new EngineResult { TotalExecutionTime = stopwatch.Elapsed };
        }

        var results = await ExecuteRulesInParallel(allRules, context, cancellationToken);
        stopwatch.Stop();

        return new EngineResult
        {
            RuleResults = results,
            TotalExecutionTime = stopwatch.Elapsed
        };
    }

    /// <inheritdoc />
    public async Task<EngineResult> ExecuteByCategoryAsync(
        IArchitectureContext context, 
        IEnumerable<string> categories, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var categorySet = categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredRules = _rules.Where(r => categorySet.Contains(r.Category)).ToList();
        
        if (!filteredRules.Any())
        {
            return new EngineResult { TotalExecutionTime = stopwatch.Elapsed };
        }

        var results = await ExecuteRulesInParallel(filteredRules, context, cancellationToken);
        stopwatch.Stop();

        return new EngineResult
        {
            RuleResults = results,
            TotalExecutionTime = stopwatch.Elapsed
        };
    }

    private async Task<IReadOnlyList<RuleResult>> ExecuteRulesInParallel(
        IReadOnlyList<IArchitectureRule> rules,
        IArchitectureContext context,
        CancellationToken cancellationToken)
    {
        var maxDegreeOfParallelism = context.Configuration.Execution.MaxDegreeOfParallelism;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        var results = new ConcurrentBag<RuleResult>();

        await Parallel.ForEachAsync(rules, parallelOptions, async (rule, ct) =>
        {
            try
            {
                var result = await rule.ValidateAsync(context, ct);
                results.Add(result);
                
                // Fail fast on critical errors if configured
                if (context.Configuration.Execution.FailFastOnCritical && 
                    rule.Severity == RuleSeverity.Critical && 
                    !result.IsSuccess)
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                results.Add(RuleResult.FromError(rule.RuleId, ex.Message, TimeSpan.Zero));
            }
        });

        return results.ToList();
    }
}