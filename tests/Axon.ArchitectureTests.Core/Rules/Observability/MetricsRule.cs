using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Observability;

/// <summary>
/// Rule to validate metrics collection and implementation patterns.
/// </summary>
public sealed class MetricsRule : ArchitectureRuleBase
{
    public override string RuleId => "OBS_002";
    public override string Name => "Metrics Implementation";
    public override string Description => "Validates metrics collection patterns and best practices";
    public override string Category => "Observability";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var assembly in context.Assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (var type in types)
                {
                    ValidateMetricsUsage(type, violations);
                    ValidateCounterPatterns(type, violations);
                    ValidatePerformanceMetrics(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateMetricsUsage(Type type, List<RuleViolation> violations)
    {
        if (!IsServiceClass(type)) return;

        if (IsBusinessService(type) && !HasMetricsCollection(type))
        {
            violations.Add(CreateViolation(type,
                $"Business service '{type.Name}' should collect metrics for monitoring",
                "Add metrics collection for business operations and performance monitoring"));
        }
    }

    private void ValidateCounterPatterns(Type type, List<RuleViolation> violations)
    {
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var field in fields)
        {
            if (IsCounterField(field) && !field.IsInitOnly)
            {
                violations.Add(CreateViolation(type,
                    $"Counter field '{field.Name}' should be readonly",
                    "Make counter fields readonly to prevent reassignment"));
            }
        }
    }

    private void ValidatePerformanceMetrics(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            if (IsPerformanceCriticalMethod(method) && !HasDurationMetrics(method))
            {
                violations.Add(CreateViolation(type,
                    $"Performance-critical method '{method.Name}' should measure execution duration",
                    "Add duration/timing metrics using Histogram or Timer"));
            }
        }
    }

    private static bool IsServiceClass(Type type) =>
        type.Name.EndsWith("Service") || type.Name.EndsWith("Handler") || type.Name.EndsWith("Controller");

    private static bool IsBusinessService(Type type) =>
        type.Name.EndsWith("Service") && !type.Name.Contains("Infrastructure");

    private static bool HasMetricsCollection(Type type) =>
        type.GetFields().Any(f => IsMetricsType(f.FieldType));

    private static bool IsMetricsType(Type paramType) =>
        paramType.Name.Contains("Meter") || paramType.Name.Contains("Counter");

    private static bool IsCounterField(FieldInfo field) =>
        field.FieldType.Name.Contains("Counter");

    private static bool IsPerformanceCriticalMethod(MethodInfo method) =>
        method.IsPublic && method.ReturnType.Name.Contains("Task");

    private static bool HasDurationMetrics(MethodInfo method) =>
        method.Name.Contains("Duration", StringComparison.OrdinalIgnoreCase);
}