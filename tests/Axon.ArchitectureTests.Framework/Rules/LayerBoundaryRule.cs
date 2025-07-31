using Axon.ArchitectureTests.Framework.Contracts;

namespace Axon.ArchitectureTests.Framework.Rules;

/// <summary>
/// Base class for rules that validate layer boundaries in Clean Architecture.
/// </summary>
public abstract class LayerBoundaryRule : ArchitectureRuleBase
{
    private readonly string _sourceLayer;
    private readonly string[] _allowedTargetLayers;
    private readonly string[] _forbiddenTargetLayers;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayerBoundaryRule"/> class.
    /// </summary>
    /// <param name="sourceLayer">The source layer namespace pattern.</param>
    /// <param name="allowedTargetLayers">Allowed target layer namespace patterns.</param>
    /// <param name="forbiddenTargetLayers">Forbidden target layer namespace patterns.</param>
    protected LayerBoundaryRule(string sourceLayer, string[] allowedTargetLayers, string[]? forbiddenTargetLayers = null)
    {
        _sourceLayer = sourceLayer;
        _allowedTargetLayers = allowedTargetLayers;
        _forbiddenTargetLayers = forbiddenTargetLayers ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    public override string Category => "LayerBoundary";

    /// <inheritdoc />
    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var sourceTypes = context.Types.Where(t => IsInLayer(t, _sourceLayer)).ToList();

        await Task.Run(() =>
        {
            foreach (var sourceType in sourceTypes)
            {
                var dependencies = GetTypeDependencies(sourceType);
                
                foreach (var dependency in dependencies)
                {
                    if (IsForbiddenDependency(dependency))
                    {
                        violations.Add(CreateViolation(
                            sourceType,
                            $"Type '{sourceType.FullName}' in {_sourceLayer} layer has forbidden dependency on '{dependency.FullName}' in {GetLayerName(dependency)} layer",
                            GetSuggestedFix(sourceType, dependency)));
                    }
                }
            }
        }, cancellationToken);

        return violations;
    }

    /// <summary>
    /// Gets all type dependencies for the given type.
    /// </summary>
    protected virtual IEnumerable<Type> GetTypeDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();
        
        // Base type dependencies
        if (type.BaseType != null && !IsSystemType(type.BaseType))
            dependencies.Add(type.BaseType);
            
        // Interface dependencies
        foreach (var interfaceType in type.GetInterfaces().Where(i => !IsSystemType(i)))
            dependencies.Add(interfaceType);
            
        // Field dependencies
        foreach (var field in type.GetFields())
            if (!IsSystemType(field.FieldType))
                dependencies.Add(field.FieldType);
                
        return dependencies;
    }

    private bool IsInLayer(Type type, string layerPattern) =>
        type.Namespace?.Contains(layerPattern, StringComparison.OrdinalIgnoreCase) == true;

    private bool IsForbiddenDependency(Type dependency)
    {
        var dependencyLayer = GetLayerName(dependency);
        
        // Check if explicitly forbidden
        if (_forbiddenTargetLayers.Any(forbidden => dependencyLayer.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            return true;
            
        // Check if not in allowed layers
        return !_allowedTargetLayers.Any(allowed => dependencyLayer.Contains(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private string GetLayerName(Type type) => type.Namespace ?? "Unknown";

    private static bool IsSystemType(Type type) => 
        type.Namespace?.StartsWith("System", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Gets a suggested fix for the violation.
    /// </summary>
    protected virtual string? GetSuggestedFix(Type sourceType, Type dependency) =>
        $"Move '{dependency.Name}' to an appropriate layer or use dependency injection";
}