using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.Enhanced;

/// <summary>
/// Enhanced cross-layer dependency validation rule that analyzes complex dependency patterns.
/// Validates that dependencies flow in the correct direction and identifies circular dependencies.
/// </summary>
public sealed class CrossLayerDependencyRule : PatternComplianceRule
{
    public override string RuleId => "ECA001";
    public override string Name => "Cross-Layer Dependency Rule";
    public override string Description => "Validates that dependencies flow correctly across architecture layers and prevents circular dependencies";
    public override string Category => "Enhanced Clean Architecture";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    private static readonly Dictionary<string, int> LayerHierarchy = new()
    {
        { "Api", 1 },
        { "Application", 2 },
        { "Domain", 3 },
        { "Infrastructure", 2 }, // Same level as Application, but can depend on Application
        { "Shared", 4 } // Lowest level, can be referenced by all
    };

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var typeAnalysis = AnalyzeTypeDependencies(context.Types);
            
            ValidateLayerDependencyDirection(typeAnalysis.Dependencies, violations);
            ValidateCircularDependencies(typeAnalysis.Dependencies, violations);
            ValidateForbiddenDependencies(typeAnalysis.Dependencies, violations);
            ValidateTransitiveDependencies(typeAnalysis.Dependencies, violations);
            
        }, cancellationToken);

        return violations;
    }

    private TypeDependencyAnalysis AnalyzeTypeDependencies(IEnumerable<Type> types)
    {
        var dependencies = new Dictionary<Type, HashSet<Type>>();
        var layerMapping = new Dictionary<Type, string>();

        foreach (var type in types.Where(t => t.Namespace != null))
        {
            var layer = DetermineLayer(type);
            if (layer != null)
            {
                layerMapping[type] = layer;
                dependencies[type] = GetDirectDependencies(type);
            }
        }

        return new TypeDependencyAnalysis
        {
            Dependencies = dependencies,
            LayerMapping = layerMapping
        };
    }

    private static string? DetermineLayer(Type type)
    {
        var namespaceParts = type.Namespace?.Split('.');
        if (namespaceParts == null) return null;

        foreach (var part in namespaceParts)
        {
            if (LayerHierarchy.ContainsKey(part))
                return part;
        }

        return null;
    }

    private HashSet<Type> GetDirectDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();

        // Analyze constructor dependencies
        foreach (var constructor in GetConstructors(type))
        {
            foreach (var param in constructor.GetParameters())
            {
                if (param.ParameterType.Namespace?.StartsWith("Axon") == true)
                {
                    dependencies.Add(param.ParameterType);
                }
            }
        }

        // Analyze property dependencies
        foreach (var property in GetPublicProperties(type))
        {
            if (property.PropertyType.Namespace?.StartsWith("Axon") == true)
            {
                dependencies.Add(property.PropertyType);
            }
        }

        // Analyze method parameter dependencies
        foreach (var method in type.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType.Namespace?.StartsWith("Axon") == true)
                {
                    dependencies.Add(param.ParameterType);
                }
            }

            // Analyze return type
            if (method.ReturnType.Namespace?.StartsWith("Axon") == true)
            {
                dependencies.Add(method.ReturnType);
            }
        }

        return dependencies;
    }

    private void ValidateLayerDependencyDirection(
        Dictionary<Type, HashSet<Type>> dependencies, 
        List<RuleViolation> violations)
    {
        foreach (var (type, deps) in dependencies)
        {
            var sourceLayer = DetermineLayer(type);
            if (sourceLayer == null) continue;

            foreach (var dependency in deps)
            {
                var targetLayer = DetermineLayer(dependency);
                if (targetLayer == null) continue;

                if (IsInvalidDependencyDirection(sourceLayer, targetLayer))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Invalid dependency direction: {sourceLayer} layer depends on {targetLayer} layer",
                        $"Remove dependency from {type.Name} to {dependency.Name} or restructure layers"));
                }
            }
        }
    }

    private static bool IsInvalidDependencyDirection(string sourceLayer, string targetLayer)
    {
        // Special cases
        if (targetLayer == "Shared") return false; // All layers can depend on Shared
        if (sourceLayer == "Infrastructure" && targetLayer == "Application") return false; // Infrastructure can depend on Application

        if (!LayerHierarchy.TryGetValue(sourceLayer, out var sourceLevel) ||
            !LayerHierarchy.TryGetValue(targetLayer, out var targetLevel))
        {
            return false;
        }

        // Dependencies should flow towards higher numbers (deeper layers)
        return sourceLevel < targetLevel;
    }

    private void ValidateCircularDependencies(
        Dictionary<Type, HashSet<Type>> dependencies, 
        List<RuleViolation> violations)
    {
        var visited = new HashSet<Type>();
        var recursionStack = new HashSet<Type>();

        foreach (var type in dependencies.Keys)
        {
            if (!visited.Contains(type))
            {
                DetectCircularDependency(type, dependencies, visited, recursionStack, violations);
            }
        }
    }

    private bool DetectCircularDependency(
        Type current,
        Dictionary<Type, HashSet<Type>> dependencies,
        HashSet<Type> visited,
        HashSet<Type> recursionStack,
        List<RuleViolation> violations)
    {
        visited.Add(current);
        recursionStack.Add(current);

        if (dependencies.TryGetValue(current, out var currentDeps))
        {
            foreach (var dependency in currentDeps)
            {
                if (!visited.Contains(dependency))
                {
                    if (DetectCircularDependency(dependency, dependencies, visited, recursionStack, violations))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(dependency))
                {
                    violations.Add(CreateViolation(
                        current,
                        $"Circular dependency detected: {current.Name} -> {dependency.Name}",
                        $"Break circular dependency by introducing abstraction or restructuring"));
                    return true;
                }
            }
        }

        recursionStack.Remove(current);
        return false;
    }

    private void ValidateForbiddenDependencies(
        Dictionary<Type, HashSet<Type>> dependencies, 
        List<RuleViolation> violations)
    {
        var forbiddenPatterns = new[]
        {
            ("Domain", "Infrastructure"),
            ("Domain", "Api"),
            ("Application", "Api"),
            ("Application", "Infrastructure") // Application should only depend on Infrastructure abstractions
        };

        foreach (var (type, deps) in dependencies)
        {
            var sourceLayer = DetermineLayer(type);
            if (sourceLayer == null) continue;

            foreach (var dependency in deps)
            {
                var targetLayer = DetermineLayer(dependency);
                if (targetLayer == null) continue;

                if (forbiddenPatterns.Any(p => p.Item1 == sourceLayer && p.Item2 == targetLayer))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Forbidden dependency: {sourceLayer} layer must not depend on {targetLayer} layer",
                        $"Remove dependency from {type.Name} to {dependency.Name} and use dependency inversion"));
                }
            }
        }
    }

    private void ValidateTransitiveDependencies(
        Dictionary<Type, HashSet<Type>> dependencies, 
        List<RuleViolation> violations)
    {
        foreach (var (type, _) in dependencies)
        {
            var transitiveDeps = GetTransitiveDependencies(type, dependencies, new HashSet<Type>());
            var chainLength = CalculateMaxDependencyChainLength(type, dependencies);

            if (chainLength > 5) // Configurable threshold
            {
                violations.Add(CreateViolation(
                    type,
                    $"Deep dependency chain detected (length: {chainLength})",
                    $"Reduce dependency chain length for {type.Name} by introducing intermediate abstractions"));
            }

            if (transitiveDeps.Count > 20) // Configurable threshold
            {
                violations.Add(CreateViolation(
                    type,
                    $"High transitive dependency count: {transitiveDeps.Count}",
                    $"Consider reducing coupling for {type.Name}"));
            }
        }
    }

    private static HashSet<Type> GetTransitiveDependencies(
        Type type, 
        Dictionary<Type, HashSet<Type>> dependencies, 
        HashSet<Type> visited)
    {
        if (visited.Contains(type)) return new HashSet<Type>();
        
        visited.Add(type);
        var transitive = new HashSet<Type>();

        if (dependencies.TryGetValue(type, out var directDeps))
        {
            foreach (var dep in directDeps)
            {
                transitive.Add(dep);
                var depTransitive = GetTransitiveDependencies(dep, dependencies, visited);
                transitive.UnionWith(depTransitive);
            }
        }

        visited.Remove(type);
        return transitive;
    }

    private static int CalculateMaxDependencyChainLength(
        Type type, 
        Dictionary<Type, HashSet<Type>> dependencies, 
        HashSet<Type> visited = null!)
    {
        visited ??= new HashSet<Type>();
        
        if (visited.Contains(type)) return 0;
        visited.Add(type);

        var maxLength = 0;
        if (dependencies.TryGetValue(type, out var deps))
        {
            foreach (var dep in deps)
            {
                var depLength = CalculateMaxDependencyChainLength(dep, dependencies, visited);
                maxLength = Math.Max(maxLength, depLength + 1);
            }
        }

        visited.Remove(type);
        return maxLength;
    }

    private sealed class TypeDependencyAnalysis
    {
        public Dictionary<Type, HashSet<Type>> Dependencies { get; set; } = new();
        public Dictionary<Type, string> LayerMapping { get; set; } = new();
    }
}