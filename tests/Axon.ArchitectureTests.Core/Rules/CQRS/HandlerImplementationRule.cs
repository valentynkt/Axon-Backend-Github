using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using MediatR;

namespace Axon.ArchitectureTests.Core.Rules.CQRS;

/// <summary>
/// Validates that Handlers are properly implemented following CQRS patterns.
/// </summary>
public sealed class HandlerImplementationRule : PatternComplianceRule
{
    public override string RuleId => "CQRS003";
    public override string Name => "Handler Implementation Rule";
    public override string Description => "Handlers must implement IRequestHandler<T,R>, be sealed, follow naming conventions, and have proper async signatures";
    public override string Category => "CQRS";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var handlerTypes = context.Types
                .Where(t => IsHandlerType(t))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                ValidateHandlerNaming(handlerType, violations);
                ValidateHandlerStructure(handlerType, violations);
                ValidateHandlerLocation(handlerType, violations);
                ValidateHandlerMediatRInterface(handlerType, violations);
                ValidateHandlerMethods(handlerType, violations);
                ValidateHandlerDependencies(handlerType, violations);
                ValidateHandlerErrorHandling(handlerType, violations);
                ValidateHandlerSingleResponsibility(handlerType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsHandlerType(Type type) =>
        type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
        ImplementsAnyHandlerInterface(type);

    private static bool ImplementsAnyHandlerInterface(Type type) =>
        type.GetInterfaces().Any(i => 
            i.IsGenericType && 
            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || 
             i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)));

    private void ValidateHandlerNaming(Type handlerType, List<RuleViolation> violations)
    {
        if (!handlerType.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handler types must end with 'Handler' suffix",
                $"Rename '{handlerType.Name}' to '{handlerType.Name}Handler'"));
        }
    }

    private void ValidateHandlerStructure(Type handlerType, List<RuleViolation> violations)
    {
        if (!IsSealed(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers should be sealed to prevent inheritance",
                $"Make '{handlerType.Name}' sealed"));
        }
    }

    private void ValidateHandlerLocation(Type handlerType, List<RuleViolation> violations)
    {
        var isInHandlersNamespace = handlerType.Namespace?.Contains(".Handlers.", StringComparison.OrdinalIgnoreCase) == true ||
                                   handlerType.Namespace?.EndsWith(".Handlers", StringComparison.OrdinalIgnoreCase) == true;

        if (!isInHandlersNamespace)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers should be placed in a Handlers namespace",
                $"Move '{handlerType.Name}' to a .Handlers namespace"));
        }
    }

    private void ValidateHandlerMediatRInterface(Type handlerType, List<RuleViolation> violations)
    {
        var implementsHandler = ImplementsGenericInterface(handlerType, typeof(IRequestHandler<,>)) ||
                               ImplementsGenericInterface(handlerType, typeof(IRequestHandler<>));

        if (!implementsHandler)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers must implement IRequestHandler<T> or IRequestHandler<T,R> from MediatR",
                $"Make '{handlerType.Name}' implement appropriate IRequestHandler interface"));
        }
    }

    private void ValidateHandlerMethods(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = GetMethodsWithName(handlerType, "Handle");
        
        foreach (var method in handleMethods)
        {
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handler method '{method.Name}' should be async and return Task or Task<T>",
                    $"Make '{method.Name}' async and return Task or Task<T>"));
            }
            
            var parameters = method.GetParameters();
            if (parameters.Length > 0 && !parameters.Any(p => p.ParameterType == typeof(CancellationToken)))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handler method '{method.Name}' should accept CancellationToken parameter",
                    $"Add CancellationToken parameter to '{method.Name}' method"));
            }
        }
    }

    /// <summary>
    /// Validates that handlers have appropriate dependency injection patterns.
    /// </summary>
    private void ValidateHandlerDependencies(Type handlerType, List<RuleViolation> violations)
    {
        var constructors = GetConstructors(handlerType);
        var primaryConstructor = constructors.FirstOrDefault(c => c.IsPublic);

        if (primaryConstructor == null)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers must have a public constructor for dependency injection",
                $"Add a public constructor to '{handlerType.Name}'"));
            return;
        }

        var parameters = primaryConstructor.GetParameters();
        
        // Check for too many dependencies (potential SRP violation)
        if (parameters.Length > 5)
        {
            violations.Add(CreateViolation(
                handlerType,
                $"Handler has {parameters.Length} dependencies, which may indicate SRP violation",
                $"Consider reducing dependencies in '{handlerType.Name}' or splitting responsibilities"));
        }

        // Check for dependencies on other handlers (anti-pattern)
        foreach (var param in parameters)
        {
            if (param.ParameterType.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handler depends on another handler '{param.ParameterType.Name}', which is an anti-pattern",
                    $"Use domain services or extract shared logic instead of depending on other handlers"));
            }
        }
    }

    /// <summary>
    /// Validates that handlers properly handle errors and use Result patterns.
    /// </summary>
    private void ValidateHandlerErrorHandling(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = GetMethodsWithName(handlerType, "Handle");
        
        foreach (var method in handleMethods)
        {
            var returnType = method.ReturnType;
            
            // Check if async method returns proper Task type
            if (IsAsyncMethod(method))
            {
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var taskResultType = returnType.GetGenericArguments()[0];
                    
                    // Encourage Result pattern usage
                    if (!IsResultType(taskResultType) && !IsUnitType(taskResultType))
                    {
                        violations.Add(CreateViolation(
                            handlerType,
                            $"Handler method returns '{taskResultType.Name}' instead of Result pattern",
                            $"Consider wrapping return type in Result<T> for better error handling"));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that handlers follow single responsibility principle.
    /// </summary>
    private void ValidateHandlerSingleResponsibility(Type handlerType, List<RuleViolation> violations)
    {
        var allMethods = handlerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == handlerType)
            .ToList();

        // Handlers should primarily have Handle method
        var nonHandleMethods = allMethods.Where(m => !m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (nonHandleMethods.Count > 2) // Allow a couple of private helper methods
        {
            violations.Add(CreateViolation(
                handlerType,
                $"Handler has {nonHandleMethods.Count} non-Handle methods, potentially violating SRP",
                $"Consider extracting business logic from '{handlerType.Name}' into domain services"));
        }

        // Check method complexity by line count (approximate)
        foreach (var method in GetMethodsWithName(handlerType, "Handle"))
        {
            // This is a heuristic - in real scenarios you might want to use Roslyn for accurate analysis
            var methodBody = method.GetMethodBody();
            if (methodBody?.GetILAsByteArray()?.Length > 1000) // Rough approximation
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method in '{handlerType.Name}' is complex and may violate SRP",
                    $"Consider breaking down the Handle method or extracting domain services"));
            }
        }
    }

    /// <summary>
    /// Checks if a type is a Result wrapper type.
    /// </summary>
    private static bool IsResultType(Type type)
    {
        return type.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace?.Contains("Result") == true;
    }

    /// <summary>
    /// Checks if a type is the MediatR Unit type.
    /// </summary>
    private static bool IsUnitType(Type type)
    {
        return type.Name == "Unit" && type.Namespace?.Contains("MediatR") == true;
    }

    private static IEnumerable<System.Reflection.MethodInfo> GetMethodsWithName(Type type, string methodName) =>
        type.GetMethods().Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
}