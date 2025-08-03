using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Enhanced;

/// <summary>
/// Validates that the Dependency Inversion Principle is properly applied across layers.
/// Ensures high-level modules don't depend on low-level modules, both should depend on abstractions.
/// </summary>
public sealed class DependencyInversionRule : PatternComplianceRule
{
    public override string RuleId => "ECA002";
    public override string Name => "Dependency Inversion Principle Rule";
    public override string Description => "Validates that high-level modules depend on abstractions, not concrete implementations";
    public override string Category => "Enhanced Clean Architecture";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var applicationTypes = GetApplicationLayerTypes(context.Types);
            var infrastructureTypes = GetInfrastructureLayerTypes(context.Types);
            
            ValidateApplicationLayerDependencies(applicationTypes, violations);
            ValidateInfrastructureLayerDependencies(infrastructureTypes, violations);
            ValidateAbstractionUsage(context.Types, violations);
            ValidateConcreteDependencies(context.Types, violations);
            
        }, cancellationToken);

        return violations;
    }

    private static IEnumerable<Type> GetApplicationLayerTypes(IEnumerable<Type> types) =>
        types.Where(t => IsInApplicationLayer(t));

    private static IEnumerable<Type> GetInfrastructureLayerTypes(IEnumerable<Type> types) =>
        types.Where(IsInInfrastructureLayer);

    private static bool IsInApplicationLayer(Type type) =>
        type.Namespace?.Contains(".Application.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInInfrastructureLayer(Type type) =>
        type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateApplicationLayerDependencies(IEnumerable<Type> applicationTypes, List<RuleViolation> violations)
    {
        foreach (var type in applicationTypes)
        {
            // Application layer should depend on interfaces/abstractions
            var concreteDependencies = GetConcreteDependencies(type);
            
            foreach (var dependency in concreteDependencies)
            {
                if (IsInfrastructureConcrete(dependency))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Application layer type '{type.Name}' depends on concrete infrastructure type '{dependency.Name}'",
                        $"Replace concrete dependency with abstraction/interface"));
                }
            }

            // Check constructor dependencies
            ValidateConstructorDependencies(type, violations);
        }
    }

    private void ValidateInfrastructureLayerDependencies(IEnumerable<Type> infrastructureTypes, List<RuleViolation> violations)
    {
        foreach (var type in infrastructureTypes)
        {
            // Infrastructure should implement application interfaces
            if (IsConcreteImplementation(type))
            {
                var implementedInterfaces = GetImplementedApplicationInterfaces(type);
                
                if (!implementedInterfaces.Any() && !IsConfigurationOrStartupType(type))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Infrastructure concrete type '{type.Name}' should implement an application interface",
                        $"Create and implement an interface in the Application layer"));
                }
            }

            // Validate that infrastructure doesn't create tight coupling
            ValidateInfrastructureCoupling(type, violations);
        }
    }

    private void ValidateConstructorDependencies(Type type, List<RuleViolation> violations)
    {
        foreach (var constructor in GetConstructors(type))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (IsConcreteType(parameter.ParameterType) && 
                    !IsAllowedConcreteDependency(parameter.ParameterType))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Constructor of '{type.Name}' depends on concrete type '{parameter.ParameterType.Name}' instead of abstraction",
                        $"Replace parameter type with interface or abstract class"));
                }
            }
        }
    }

    private void ValidateAbstractionUsage(IEnumerable<Type> types, List<RuleViolation> violations)
    {
        var abstractionTypes = types.Where(IsAbstraction).ToList();
        var concreteTypes = types.Where(t => !IsAbstraction(t)).ToList();

        foreach (var abstraction in abstractionTypes)
        {
            var implementations = concreteTypes.Where(t => ImplementsInterface(t, abstraction) || 
                                                         InheritsFrom(t, abstraction)).ToList();

            // Abstractions should have at least one implementation
            if (!implementations.Any() && ShouldHaveImplementation(abstraction))
            {
                violations.Add(CreateViolation(
                    abstraction,
                    $"Abstraction '{abstraction.Name}' has no concrete implementations",
                    $"Provide concrete implementation or remove unused abstraction"));
            }

            // Check for proper interface segregation
            ValidateInterfaceDesign(abstraction, violations);
        }
    }

    private void ValidateConcreteDependencies(IEnumerable<Type> types, List<RuleViolation> violations)
    {
        foreach (var type in types.Where(t => !IsInfrastructureLayer(t)))
        {
            var dependencies = GetAllDependencies(type);
            
            foreach (var dependency in dependencies)
            {
                if (IsConcreteType(dependency) && 
                    IsInfrastructureLayer(dependency) && 
                    !IsAllowedConcreteDependency(dependency))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Type '{type.Name}' has concrete dependency on infrastructure type '{dependency.Name}'",
                        $"Use dependency injection with abstraction instead of concrete dependency"));
                }
            }
        }
    }

    private void ValidateInfrastructureCoupling(Type type, List<RuleViolation> violations)
    {
        var dependencies = GetAllDependencies(type);
        var externalDependencies = dependencies.Where(IsExternalDependency).ToList();
        
        if (externalDependencies.Count > 5) // Configurable threshold
        {
            violations.Add(CreateViolation(
                type,
                $"Infrastructure type '{type.Name}' has too many external dependencies ({externalDependencies.Count})",
                $"Consider using facade pattern or breaking into smaller components"));
        }

        // Check for tight coupling patterns
        foreach (var dependency in dependencies)
        {
            if (IsStaticDependency(type, dependency))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Type '{type.Name}' uses static dependency '{dependency.Name}'",
                    $"Replace static usage with dependency injection"));
            }
        }
    }

    private void ValidateInterfaceDesign(Type interfaceType, List<RuleViolation> violations)
    {
        if (!interfaceType.IsInterface) return;

        var methods = interfaceType.GetMethods();
        
        // Interface Segregation Principle - interfaces should be focused
        if (methods.Length > 10) // Configurable threshold
        {
            violations.Add(CreateViolation(
                interfaceType,
                $"Interface '{interfaceType.Name}' has too many methods ({methods.Length})",
                $"Consider breaking into smaller, more focused interfaces"));
        }

        // Check for cohesion
        ValidateInterfaceCohesion(interfaceType, methods, violations);
    }

    private void ValidateInterfaceCohesion(Type interfaceType, MethodInfo[] methods, List<RuleViolation> violations)
    {
        var methodGroups = GroupMethodsByPurpose(methods);
        
        if (methodGroups.Count > 3) // Multiple distinct purposes
        {
            violations.Add(CreateViolation(
                interfaceType,
                $"Interface '{interfaceType.Name}' appears to serve multiple purposes",
                $"Consider splitting into focused interfaces based on client needs"));
        }
    }

    private static Dictionary<string, List<MethodInfo>> GroupMethodsByPurpose(MethodInfo[] methods)
    {
        var groups = new Dictionary<string, List<MethodInfo>>();
        
        foreach (var method in methods)
        {
            var purpose = DeterminePurpose(method);
            if (!groups.TryGetValue(purpose, out List<MethodInfo>? value))
            {
                value = new List<MethodInfo>();
                groups[purpose] = value;
            }

            value.Add(method);
        }
        
        return groups;
    }

    private static string DeterminePurpose(MethodInfo method)
    {
        var name = method.Name.ToLowerInvariant();
        
        if (name.StartsWith("get") || name.StartsWith("find") || name.StartsWith("query"))
            return "Query";
        if (name.StartsWith("create") || name.StartsWith("add") || name.StartsWith("insert"))
            return "Create";
        if (name.StartsWith("update") || name.StartsWith("modify") || name.StartsWith("edit"))
            return "Update";
        if (name.StartsWith("delete") || name.StartsWith("remove"))
            return "Delete";
        if (name.StartsWith("validate") || name.StartsWith("check"))
            return "Validation";
        
        return "Other";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public IEnumerable<Type> GetConcreteDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();
        
        // Constructor parameters
        foreach (var constructor in GetConstructors(type))
        {
            foreach (var param in constructor.GetParameters())
            {
                if (IsConcreteType(param.ParameterType))
                {
                    dependencies.Add(param.ParameterType);
                }
            }
        }
        
        return dependencies;
    }

    public IEnumerable<Type> GetAllDependencies(Type type)
    {
        var dependencies = new HashSet<Type>();
        
        // Constructor, property, field, and method dependencies
        foreach (var constructor in GetConstructors(type))
        {
            foreach (var param in constructor.GetParameters())
            {
                dependencies.Add(param.ParameterType);
            }
        }
        
        foreach (var property in GetPublicProperties(type))
        {
            dependencies.Add(property.PropertyType);
        }
        
        return dependencies;
    }

    private static IEnumerable<Type> GetImplementedApplicationInterfaces(Type type)
    {
        return type.GetInterfaces()
            .Where(i => IsInApplicationLayer(i));
    }

    private static bool IsInfrastructureConcrete(Type type) =>
        IsInfrastructureLayer(type) && IsConcreteType(type);

    private static bool IsConcreteImplementation(Type type) =>
        type.IsClass && !type.IsAbstract && !IsConfigurationOrStartupType(type);

    private bool IsAbstraction(Type type) =>
        type.IsInterface || type.IsAbstract;

    private static bool IsConcreteType(Type type) =>
        type.IsClass && !type.IsAbstract && !type.IsInterface;

    private static bool IsAllowedConcreteDependency(Type type) =>
        type.IsPrimitive || 
        type == typeof(string) || 
        type.Namespace?.StartsWith("System") == true ||
        IsValueType(type) ||
        IsConfigurationOrStartupType(type);

    private static bool IsConfigurationOrStartupType(Type type) =>
        type.Name.EndsWith("Options") ||
        type.Name.EndsWith("Settings") ||
        type.Name.EndsWith("Configuration") ||
        type.Name.Contains("Startup");

    private static bool ShouldHaveImplementation(Type abstraction) =>
        abstraction.IsInterface && 
        !abstraction.Name.StartsWith($"I") || // Consider marker interfaces
        abstraction.GetMethods().Any(); // Has methods to implement

    private bool IsExternalDependency(Type type) =>
        !type.Namespace?.StartsWith("Axon") == true;

    private static bool IsStaticDependency(Type type, Type dependency)
    {
        // Check for static method calls (simplified heuristic)
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return methods.Any(m => m.Name.Contains(dependency.Name));
    }

    private static bool IsValueType(Type type) =>
        type.IsValueType || 
        (type.Namespace?.StartsWith("Axon") == true && 
         (type.Name.EndsWith("Id") || type.Name.EndsWith("Value")));

    private static bool InheritsFrom(Type type, Type baseType) =>
        type.IsSubclassOf(baseType) || 
        (baseType.IsInterface && baseType.IsAssignableFrom(type));

    private static bool IsInfrastructureLayer(Type type) =>
        type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;
}