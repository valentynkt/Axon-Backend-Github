using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;

namespace Axon.ArchitectureTests.Core.Rules.CleanArchitecture;

/// <summary>
/// Validates that the API layer only depends on Application layer and Shared components.
/// </summary>
public sealed class ApiLayerDependencyRule : LayerBoundaryRule
{
    public ApiLayerDependencyRule() : base(
        sourceLayer: "Api",
        allowedTargetLayers: new[] { "Application", "Shared" },
        forbiddenTargetLayers: new[] { "Domain", "Infrastructure" })
    {
    }

    public override string RuleId => "CA001";
    public override string Name => "API Layer Dependency Rule";
    public override string Description => "API layer should only depend on Application layer and Shared components, never directly on Domain or Infrastructure";

    protected override string? GetSuggestedFix(Type sourceType, Type dependency) =>
        dependency.Namespace?.Contains("Domain", StringComparison.OrdinalIgnoreCase) == true
            ? "Move domain logic to Application layer or inject via interface"
            : dependency.Namespace?.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) == true
                ? "Use dependency injection to access infrastructure services through interfaces"
                : base.GetSuggestedFix(sourceType, dependency);

    /// <summary>
    /// Additional validation for cross-module references in API layer.
    /// </summary>
    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var baseViolations = await base.ExecuteValidationAsync(context, cancellationToken);
        var additionalViolations = new List<RuleViolation>();

        await Task.Run(() =>
        {
            var apiTypes = context.Types
                .Where(t => t.Namespace?.Contains("Api", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            foreach (var apiType in apiTypes)
            {
                ValidateCrossModuleReferences(apiType, additionalViolations);
                ValidateControllerPatterns(apiType, additionalViolations);
            }
        }, cancellationToken);

        return baseViolations.Concat(additionalViolations);
    }

    private void ValidateCrossModuleReferences(Type apiType, List<RuleViolation> violations)
    {
        // Check for direct references to other modules
        var referencedTypes = GetReferencedTypes(apiType);
        
        foreach (var referencedType in referencedTypes)
        {
            if (IsDirectModuleReference(apiType, referencedType))
            {
                violations.Add(CreateViolation(
                    apiType,
                    $"API type '{apiType.Name}' directly references '{referencedType.Name}' from another module",
                    $"Use shared contracts or application services instead of direct module references"));
            }
        }
    }

    private void ValidateControllerPatterns(Type apiType, List<RuleViolation> violations)
    {
        if (IsControllerType(apiType))
        {
            var methods = apiType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            foreach (var method in methods.Where(m => !m.IsSpecialName))
            {
                // Controllers should use MediatR for business logic
                if (!UsesMediatR(method))
                {
                    violations.Add(CreateViolation(
                        apiType,
                        $"Controller method '{method.Name}' should use MediatR for business logic delegation",
                        $"Inject IMediator and send commands/queries in '{method.Name}'"));
                }
            }
        }
    }

    private static bool IsControllerType(Type type)
    {
        return type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.BaseType?.Name.Contains("Controller", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool UsesMediatR(System.Reflection.MethodInfo method)
    {
        // Simple heuristic: check if method parameters include IMediator or method name suggests MediatR usage
        var parameters = method.GetParameters();
        return parameters.Any(p => p.ParameterType.Name.Contains("Mediator", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<Type> GetReferencedTypes(Type type)
    {
        // Simplified - get types from method parameters, return types, and fields
        var referencedTypes = new HashSet<Type>();
        
        var methods = type.GetMethods();
        foreach (var method in methods)
        {
            referencedTypes.Add(method.ReturnType);
            foreach (var param in method.GetParameters())
            {
                referencedTypes.Add(param.ParameterType);
            }
        }
        
        return referencedTypes;
    }

    private static bool IsDirectModuleReference(Type sourceType, Type referencedType)
    {
        var sourceModule = GetModuleName(sourceType);
        var referencedModule = GetModuleName(referencedType);
        
        return !string.IsNullOrEmpty(sourceModule) && 
               !string.IsNullOrEmpty(referencedModule) && 
               sourceModule != referencedModule &&
               !referencedType.Namespace?.Contains("Shared", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? GetModuleName(Type type)
    {
        // Extract module name from namespace like "Axon.Modules.Chat.Application"
        var namespaceParts = type.Namespace?.Split('.');
        if (namespaceParts?.Length > 2 && namespaceParts[1] == "Modules")
        {
            return namespaceParts[2];
        }
        return null;
    }
}